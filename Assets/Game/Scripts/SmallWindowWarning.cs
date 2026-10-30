using UnityEngine;

/// <summary>
/// A box saying the window is smaller than the game is laid out for, with an OK to close it. Shown
/// once a run: on the title menu if the game starts in a small window, or in a game the first time
/// the window is made small. Under 1280x720 the interface still works, drawn smaller (UiScale), so
/// this only warns.
/// </summary>
/// <remarks>
/// It belongs to no scene: it makes itself when the first scene loads and lives through the rest, so
/// that the title menu and a game share the once. In a game it stops time and the controls, as the
/// repair dialog's question does, and frees the pointer for the OK.
/// </remarks>
[DefaultExecutionOrder(-100)]
public class SmallWindowWarning : MonoBehaviour
{
    public const int MinWidth = 1280;
    public const int MinHeight = 720;

    private const string Text =
        "This resolution is smaller than 1280 x 720, so the interface is drawn smaller than it is meant to be. "
        + "A resolution of at least 1280 x 720 is recommended.";

    private static bool shownThisRun;
    private static bool open;
    private static int closedFrame = -10;

    private float timeScaleBefore = 1.0f;
    private bool pausedGame;
    // made on first use: not serialized, or the Editor would fill them with empty styles (FrontEnd.DrawPageTitle())
    [System.NonSerialized] private GUIStyle textStyle;
    [System.NonSerialized] private GUIStyle buttonStyle;

    /// <summary>
    /// While the box is up, and on the frame it closes and the next: the press that closed it must
    /// not also select a menu item or reach the game.
    /// </summary>
    public static bool BlocksInput => open || Time.frameCount <= closedFrame + 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject(nameof(SmallWindowWarning));
        DontDestroyOnLoad(go);
        go.AddComponent<SmallWindowWarning>();
    }

    private static bool WindowIsSmall => Screen.width < MinWidth || Screen.height < MinHeight;

    private static bool InMainMenu
    {
        get
        {
            FrontEnd frontEnd = FindAnyObjectByType<FrontEnd>();
            return frontEnd != null && frontEnd.ShowsMainMenu;
        }
    }

    /// <summary>On the title menu, or in a game outside a cutscene and the on-screen keyboard; over the save and options panel too.</summary>
    private static bool PlaceToShow()
    {
        if (InMainMenu)
        {
            return true;
        }

        PlayerObject player = PlayerObject.Player;
        return player != null && !PlayerObject.HudHidden
            && (player.controlsDisabled & (EControlMask.Cutscene | EControlMask.Keyboard)) == 0;
    }

    private void Update()
    {
        if (!open)
        {
            if (!shownThisRun && WindowIsSmall && PlaceToShow())
            {
                Open();
            }
            else if (pausedGame && Time.frameCount > closedFrame + 1)
            {
                // the controls come back a frame after the close, with the press that closed it gone
                pausedGame = false;
                PlayerObject.DisableControls(EControlMask.Warning, false);
            }
            return;
        }

        if (GameInput.CurrentKeyboard != null
            && (GameInput.CurrentKeyboard.enterKey.wasPressedThisFrame
                || GameInput.CurrentKeyboard.numpadEnterKey.wasPressedThisFrame
                || GameInput.CurrentKeyboard.spaceKey.wasPressedThisFrame
                || GameInput.CurrentKeyboard.escapeKey.wasPressedThisFrame))
        {
            Close();
        }
        else if (GameInput.CurrentGamepad != null
                 && (GameInput.CurrentGamepad.aButton.wasPressedThisFrame
                     || GameInput.CurrentGamepad.bButton.wasPressedThisFrame
                     || GameInput.CurrentGamepad.startButton.wasPressedThisFrame))
        {
            Close();
        }
    }

    private void Open()
    {
        shownThisRun = true;
        open = true;
        if (PlayerObject.Player != null && !InMainMenu)
        {
            pausedGame = true;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0.0f;
            PlayerObject.DisableControls(EControlMask.Warning, true);
        }
    }

    private void Close()
    {
        open = false;
        closedFrame = Time.frameCount;
        if (pausedGame)
        {
            Time.timeScale = timeScaleBefore;
        }
        if (GameInput.EscapePressedThisFrame())
        {
            GameInput.ConsumeEscape();
        }
    }

    private void OnGUI()
    {
        // In front of every panel; only the pointer is drawn over it.
        GUI.depth = (int)EGUIDepth.SmallWindowWarning;

        if (!open)
        {
            return;
        }

        Rect screen = new Rect(0, 0, Screen.width, Screen.height);
        GuiInput.RegisterBlockingRect(screen);

        if (textStyle == null)
        {
            Font font = FindFont();
            textStyle = new GUIStyle { font = font, fontSize = 24, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            textStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(textStyle) { fontSize = 26, wordWrap = false };
        }

        // A dark veil over everything, then the box, laid out in reference pixels (UiScale).
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(screen, Texture2D.whiteTexture);
        GUI.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);
        const float w = 640, h = 260;
        Rect box = new Rect((UiScale.Width - w) / 2, (UiScale.Height - h) / 2, w, h);
        UiScale.DrawTexture(UiScale.R(box), Texture2D.whiteTexture);
        GUI.color = previous;
        Frame(box, new Color(0.8f, 0.7f, 0.5f));

        GUI.Label(UiScale.R(box.x + 30, box.y + 20, w - 60, 140), Text, UiScale.Style(textStyle));

        Rect ok = new Rect(box.x + (w - 160) / 2, box.y + h - 80, 160, 56);
        bool hover = UiScale.R(ok).Contains(GuiInput.MousePositionGuiSpace);
        Frame(ok, hover ? Color.white : new Color(0.8f, 0.7f, 0.5f));
        buttonStyle.normal.textColor = hover ? Color.white : new Color(1f, 0.85f, 0.4f);
        GUI.Label(UiScale.R(ok), "OK", UiScale.Style(buttonStyle));

        if (GuiInput.TryConsumeClickInRect(UiScale.R(ok)))
        {
            Close();
        }

        // a click anywhere else does nothing, and goes no further
        GuiInput.TryConsumeClickInPanel(screen);
    }

    /// <summary>A two pixel frame round a rect in reference pixels.</summary>
    private static void Frame(Rect r, Color color)
    {
        const float t = 2f;
        Color previous = GUI.color;
        GUI.color = color;
        UiScale.DrawTexture(UiScale.R(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        UiScale.DrawTexture(UiScale.R(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        UiScale.DrawTexture(UiScale.R(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        UiScale.DrawTexture(UiScale.R(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// <summary>The game's own font, from the title menu or the repair dialog, whichever is there.</summary>
    private static Font FindFont()
    {
        FrontEnd frontEnd = FindAnyObjectByType<FrontEnd>();
        if (frontEnd != null && frontEnd.buttonLabelStyle != null && frontEnd.buttonLabelStyle.font != null)
        {
            return frontEnd.buttonLabelStyle.font;
        }

        if (RepairDialog.sRepairDialog != null && RepairDialog.sRepairDialog.repairDialogStyle != null)
        {
            return RepairDialog.sRepairDialog.repairDialogStyle.font;
        }

        return null;
    }
}
