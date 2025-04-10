#if UNITY_GOOGLE_DRIVE_AVAILABLE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityGoogleDrive;

namespace Naninovel
{
    public class GoogleDriveResourceLoader<TResource> : LoadResourceRunner<TResource>
        where TResource : UnityEngine.Object
    {
        public virtual string RootPath { get; }

        private static readonly Type[] nativeRequestTypes = { typeof(AudioClip), typeof(Texture2D) };

        private TResource loadedObject;
        private readonly bool useNativeRequests;
        private readonly Action<string> logAction;
        private GoogleDriveRequest downloadRequest;
        private readonly IRawConverter<TResource> converter;
        private RawDataRepresentation usedRepresentation;
        private GoogleDriveResourceProvider.CacheManifest cacheManifest;
        private byte[] rawData;

        public GoogleDriveResourceLoader (IResourceProvider provider, string rootPath, string resourcePath,
            IRawConverter<TResource> converter, Action<string> logAction) : base(provider, resourcePath)
        {
            RootPath = rootPath;
            useNativeRequests = nativeRequestTypes.Contains(typeof(TResource));
            this.logAction = logAction;

            // MP3 is not supported in native requests on the standalone platforms. Fallback to raw converters.
            #if UNITY_STANDALONE || UNITY_EDITOR
            foreach (var r in converter.Representations)
                if (WebUtils.EvaluateAudioTypeFromMime(r.MimeType) == AudioType.MPEG)
                    useNativeRequests = false;
            #endif

            this.converter = converter;
        }

        public override async UniTask Run ()
        {
            var startTime = Engine.Time.UnscaledTime;
            var usedCache = false;

            // Check if cached version of the file could be used.
            rawData = await TryLoadFileCache(Path);

            // Cached version is not valid or doesn't exist; download or export the file.
            if (rawData is null)
            {
                // 4. Load file metadata from Google Drive.
                var filePath = string.IsNullOrEmpty(RootPath) ? Path : string.Concat(RootPath, '/', Path);
                var fileMeta = await GetFileMeta(filePath);
                if (fileMeta is null) throw new Error($"Failed to resolve '{filePath}' Google Drive metadata.");

                if (converter is IGoogleDriveConverter<TResource>) rawData = await ExportFile(fileMeta);
                else rawData = await DownloadFile(fileMeta);

                // 5. Cache the downloaded file.
                await WriteFileCache(Path, fileMeta.Id, rawData);
            }
            else usedCache = true;

            // In case we used native requests the resource will already be set, so no need to use converters.
            if (!ObjectUtils.IsValid(loadedObject))
                loadedObject = await converter.Convert(rawData, System.IO.Path.GetFileNameWithoutExtension(Path));

            var result = new Resource<TResource>(Path, loadedObject);
            SetResult(result);

            logAction?.Invoke($"Resource '{Path}' loaded {StringUtils.FormatFileSize(rawData.Length)} over {Engine.Time.UnscaledTime - startTime:0.###} seconds from " + (usedCache ? "cache." : "Google Drive."));

            downloadRequest?.Dispose();
        }

        public override void Cancel ()
        {
            base.Cancel();

            if (downloadRequest != null)
            {
                downloadRequest.Abort();
                downloadRequest = null;
            }
        }

        protected virtual async UniTask<UnityGoogleDrive.Data.File> GetFileMeta (string filePath)
        {
            foreach (var representation in converter.Representations)
            {
                var fullPath = string.Concat(filePath, representation.Extension);
                var files = await Helpers.FindFilesByPathAsync(fullPath, fields: new List<string> { "files(id, mimeType, modifiedTime)" }, mime: representation.MimeType);
                if (files.Count > 1) Debug.LogWarning($"Multiple '{fullPath}' files been found in Google Drive.");
                if (files.Count > 0)
                {
                    usedRepresentation = representation;
                    return files[0];
                }
            }

            throw new Error($"Failed to retrieve '{Path}' resource from Google Drive.");
        }

        protected virtual async UniTask<byte[]> DownloadFile (UnityGoogleDrive.Data.File fileMeta)
        {
            if (useNativeRequests)
            {
                if (typeof(TResource) == typeof(AudioClip)) downloadRequest = GoogleDriveFiles.DownloadAudio(fileMeta.Id, WebUtils.EvaluateAudioTypeFromMime(fileMeta.MimeType));
                else if (typeof(TResource) == typeof(Texture2D)) downloadRequest = GoogleDriveFiles.DownloadTexture(fileMeta.Id, true);
            }
            else downloadRequest = new GoogleDriveFiles.DownloadRequest(fileMeta);

            await downloadRequest.SendNonGeneric();
            if (downloadRequest.IsError || downloadRequest.GetResponseData<UnityGoogleDrive.Data.File>().Content == null)
                throw new Error($"Failed to download {Path}{usedRepresentation.Extension} resource from Google Drive.");

            if (useNativeRequests)
            {
                if (typeof(TResource) == typeof(AudioClip)) loadedObject = downloadRequest.GetResponseData<UnityGoogleDrive.Data.AudioFile>().AudioClip as TResource;
                else if (typeof(TResource) == typeof(Texture2D)) loadedObject = downloadRequest.GetResponseData<UnityGoogleDrive.Data.TextureFile>().Texture as TResource;
            }

            return downloadRequest.GetResponseData<UnityGoogleDrive.Data.File>().Content;
        }

        protected virtual async UniTask<byte[]> ExportFile (UnityGoogleDrive.Data.File fileMeta)
        {
            Debug.Assert(converter is IGoogleDriveConverter<TResource>);

            var gDriveConverter = (IGoogleDriveConverter<TResource>)converter;

            downloadRequest = new GoogleDriveFiles.ExportRequest(fileMeta.Id, gDriveConverter?.ExportMimeType);
            await downloadRequest.SendNonGeneric();
            if (downloadRequest.IsError || downloadRequest.GetResponseData<UnityGoogleDrive.Data.File>().Content == null)
                throw new Error($"Failed to export '{Path}' resource from Google Drive.");
            return downloadRequest.GetResponseData<UnityGoogleDrive.Data.File>().Content;
        }

        protected virtual async UniTask<byte[]> TryLoadFileCache (string resourcePath)
        {
            resourcePath = resourcePath.Replace("/", GoogleDriveResourceProvider.SlashReplace);
            var filePath = string.Concat(GoogleDriveResourceProvider.CacheDirPath, "/", resourcePath);
            //if (!string.IsNullOrEmpty(usedRepresentation.Extension))
            //    filePath += string.Concat(".", usedRepresentation.Extension);
            if (!File.Exists(filePath)) return null;

            if (useNativeRequests)
            {
                // Web requests over IndexedDB are not supported; we should either use raw converters or disable caching.
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    // Binary conversion of the audio is fucked on WebGL (can't use buffers), so disable caching here.
                    if (typeof(TResource) == typeof(AudioClip)) return null;
                    // Use raw converters for other native types.
                    return await IOUtils.ReadFile(filePath);
                }

                UnityWebRequest request = null;
                if (typeof(TResource) == typeof(AudioClip)) request = UnityWebRequestMultimedia.GetAudioClip(filePath, WebUtils.EvaluateAudioTypeFromMime(converter.Representations[0].MimeType));
                else if (typeof(TResource) == typeof(Texture2D)) request = UnityWebRequestTexture.GetTexture(filePath, true);
                else return null;
                using (request)
                {
                    await request.SendWebRequest();

                    if (typeof(TResource) == typeof(AudioClip)) loadedObject = DownloadHandlerAudioClip.GetContent(request) as TResource;
                    else if (typeof(TResource) == typeof(Texture2D)) loadedObject = DownloadHandlerTexture.GetContent(request) as TResource;
                    return request.downloadHandler.data;
                }
            }
            return await IOUtils.ReadFile(filePath);
        }

        protected virtual async UniTask WriteFileCache (string resourcePath, string fileId, byte[] fileRawData)
        {
            resourcePath = resourcePath.Replace("/", GoogleDriveResourceProvider.SlashReplace);
            var filePath = string.Concat(GoogleDriveResourceProvider.CacheDirPath, "/", resourcePath);
            //if (!string.IsNullOrEmpty(usedRepresentation.Extension))
            //    filePath += string.Concat(".", usedRepresentation.Extension);

            await IOUtils.WriteFile(filePath, fileRawData);

            // Add info for the smart caching policy.
            if (cacheManifest == null) cacheManifest = await GoogleDriveResourceProvider.CacheManifest.ReadOrCreate();
            cacheManifest[fileId] = resourcePath;
            await cacheManifest.Write();
        }
    }
}

#endif
