using System;
using System.Linq;
using UnityEngine;

namespace Naninovel.UI
{
    public class TitleNewGameButton : ScriptableButton
    {
        [Tooltip("Services to exclude from state reset when starting a new game.")]
        [SerializeField] private string[] excludeFromReset = Array.Empty<string>();

        private string startScriptPath;
        private TitleMenu titleMenu;
        private IScriptPlayer player;
        private IStateManager state;
        private IScriptManager scripts;

        protected override async void Awake ()
        {
            base.Awake();

            scripts = Engine.GetServiceOrErr<IScriptManager>();
            if (!string.IsNullOrEmpty(scripts.Configuration.StartGameScript))
                startScriptPath = scripts.Configuration.StartGameScript;
            else startScriptPath = (await scripts.ScriptLoader.Locate()).FirstOrDefault();
            titleMenu = GetComponentInParent<TitleMenu>();
            player = Engine.GetServiceOrErr<IScriptPlayer>();
            state = Engine.GetServiceOrErr<IStateManager>();
            this.AssertRequiredObjects(titleMenu);
        }

        protected override void Start ()
        {
            base.Start();

            if (string.IsNullOrEmpty(startScriptPath))
                UIComponent.interactable = false;
        }

        protected override async void OnButtonClick ()
        {
            if (string.IsNullOrEmpty(startScriptPath))
            {
                Engine.Err("Can't start new game: specify start script in scripts configuration.");
                return;
            }

            await PlayTitleNewGame();
            titleMenu.Hide();
            using (await LoadingScreen.Show())
                await state.ResetState(excludeFromReset,
                    () => player.LoadAndPlay(startScriptPath));
        }

        protected virtual async UniTask PlayTitleNewGame ()
        {
            const string label = "OnNewGame";

            var scriptPath = scripts.Configuration.TitleScript;
            if (string.IsNullOrEmpty(scriptPath)) return;
            var script = (Script)await scripts.ScriptLoader.LoadOrErr(scripts.Configuration.TitleScript);
            if (!script.LabelExists(label)) return;

            player.ResetService();
            await player.LoadAndPlayAtLabel(scriptPath, label);
            await UniTask.WaitWhile(() => player.Playing);
        }
    }
}
