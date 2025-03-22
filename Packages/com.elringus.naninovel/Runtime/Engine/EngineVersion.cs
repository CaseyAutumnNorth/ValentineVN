using UnityEngine;

namespace Naninovel
{
    /// <summary>
    /// Stores engine version and build number.
    /// </summary>
    public class EngineVersion : ScriptableObject
    {
        /// <summary>
        /// Version identifier of the engine release.
        /// </summary>
        public string Version => engineVersion;
        /// <summary>
        /// Date and time the release was built.
        /// </summary>
        public string Build => buildDate;

        [SerializeField] private string engineVersion = string.Empty;
        [SerializeField, ReadOnly] private string buildDate = string.Empty;

        public static EngineVersion LoadFromResources ()
        {
            const string assetPath = nameof(EngineVersion);
            return Engine.LoadInternalResource<EngineVersion>(assetPath);
        }

        public string BuildVersionTag ()
        {
            return $"{Version}.{Build.Replace("-", "")[2..]}";
        }
    }
}
