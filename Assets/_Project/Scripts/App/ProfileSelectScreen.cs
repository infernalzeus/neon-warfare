using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NW.Board.Domain;

namespace NW.App
{
    /// <summary>
    /// Cyberpunk pilot-selection screen shown at session start.
    /// Three save-slot cards with JACK IN / INITIALIZE / WIPE actions.
    /// Fires OnSlotSelected(slotIndex) and destroys itself when a slot is chosen.
    /// </summary>
    public sealed class ProfileSelectScreen : MonoBehaviour
    {
        public Action<int> OnSlotSelected;

        Font _font;
        Text _statusLine;   // one shared line near the bottom for name-claim feedback

        static readonly Color CyanPrimary  = new Color(0.05f, 0.90f, 1.00f);
        static readonly Color CyanDim      = new Color(0.03f, 0.55f, 0.65f);
        static readonly Color MagentaAccent= new Color(0.95f, 0.10f, 0.80f);
        static readonly Color CardBgExist  = new Color(0.045f, 0.085f, 0.125f, 1f);
        static readonly Color CardBgEmpty  = new Color(0.038f, 0.052f, 0.072f, 1f);
        static readonly Color BorderExist  = new Color(0.00f, 0.82f, 1.00f, 1f);
        static readonly Color BorderEmpty  = new Color(0.20f, 0.28f, 0.38f, 1f);
        // Header strip across the top of each card, so the slot number and status read as a
        // label bar rather than floating text.
        static readonly Color HeadExist    = new Color(0.055f, 0.145f, 0.205f, 1f);
        static readonly Color HeadEmpty    = new Color(0.055f, 0.072f, 0.095f, 1f);
        // Body copy. The level line and the RESOURCES label used to be drawn in CyanDim, which
        // is darker than the card it sits on -- that is why they were unreadable.
        static readonly Color TextBody     = new Color(0.72f, 0.86f, 0.92f);
        static readonly Color TextQuiet    = new Color(0.52f, 0.66f, 0.74f);

        static readonly string[] GemShort = { "E", "P", "N", "Q", "D" };
        static readonly Color[]  GemColor =
        {
            new Color(1.00f, 0.85f, 0.10f),
            new Color(0.90f, 0.10f, 0.80f),
            new Color(0.10f, 0.90f, 0.40f),
            new Color(0.10f, 0.70f, 1.00f),
            new Color(0.85f, 0.85f, 0.90f),
        };

        public void Init(Font font)
        {
            _font = font;

            // Fullscreen dark background
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.025f, 0.04f, 0.97f);
            bg.raycastTarget = true;

            BuildGrid();
            BuildHeader();
            BuildSlotCards();
            BuildStatusLine();

            StartCoroutine(EntranceAnim());
        }

        // ─────────────────────────────── background grid ──────────────────

        void BuildGrid()
        {
            var rt = GetComponent<RectTransform>();
            Color lineCol = new Color(0.04f, 0.10f, 0.18f, 0.7f);
            int hLines = 8, vLines = 12;
            for (int i = 0; i <= hLines; i++)
            {
                float y = i / (float)hLines;
                MakeLine(rt, new Vector2(0, y), new Vector2(1, y), 1, lineCol);
            }
            for (int i = 0; i <= vLines; i++)
            {
                float x = i / (float)vLines;
                MakeLine(rt, new Vector2(x, 0), new Vector2(x, 1), 1, lineCol);
            }
        }

        // ──────────────────────────────────── header ──────────────────────

        void BuildHeader()
        {
            var rt = GetComponent<RectTransform>();

            // Title bar
            // Header took a fifth of the screen to say two lines. Cards get that space.
            var barGo = MakeRect(rt, "TitleBar",
                new Vector2(0f, 0.87f), new Vector2(1f, 1f));
            barGo.AddComponent<Image>().color = new Color(0f, 0.06f, 0.12f, 0.9f);

            var titleGo = MakeRect(barGo.GetComponent<RectTransform>(), "Title",
                new Vector2(0.02f, 0.25f), new Vector2(0.98f, 0.90f));
            var title = titleGo.AddComponent<Text>();
            title.font = _font; title.fontSize = 44; title.fontStyle = FontStyle.Bold;
            title.color = CyanPrimary;
            title.alignment = TextAnchor.MiddleCenter;
            title.text = "CHOOSE YOUR PILOT";
            title.raycastTarget = false;

            var subGo = MakeRect(barGo.GetComponent<RectTransform>(), "Sub",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.30f));
            var sub = subGo.AddComponent<Text>();
            sub.font = _font; sub.fontSize = UIScale.FontBody;
            sub.alignment = TextAnchor.MiddleCenter;
            bool dev = PlayerProgress.DevMode;
            sub.text = dev
                ? $"TESTING BUILD — Pilot {PlayerProgress.DevSlot + 1} starts fully unlocked.  Tap a name to rename it."
                : "Pick a save slot to play. Tap a name to rename it.";
            sub.color = dev ? new Color(1f, 0.72f, 0.18f) : new Color(0.58f, 0.78f, 0.86f, 1f);
            sub.raycastTarget = false;

            // Cyan divider under header
            MakeLine(rt, new Vector2(0, 0.87f), new Vector2(1, 0.87f), 2, new Color(0f, 0.85f, 1f, 0.55f));
        }

        // ─────────────────────────── name-claim status line ───────────────

        void BuildStatusLine()
        {
            var rt = GetComponent<RectTransform>();
            var go = MakeRect(rt, "StatusLine", new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.075f));
            _statusLine = go.AddComponent<Text>();
            _statusLine.font = _font; _statusLine.fontSize = UIScale.FontSmall;
            _statusLine.alignment = TextAnchor.MiddleCenter;
            _statusLine.color = TextQuiet;
            _statusLine.raycastTarget = false;
            _statusLine.text = "";
        }

        void SetStatus(string msg, Color col)
        {
            if (_statusLine == null) return;
            _statusLine.text = msg;
            _statusLine.color = col;
        }

        // ───────────────────────────────── slot cards ─────────────────────

        void BuildSlotCards()
        {
            var rt = GetComponent<RectTransform>();
            float cardW = 0.29f, gap = 0.025f;   // wider cards, tighter gutters
            float totalW = PlayerProgress.SlotCount * cardW + (PlayerProgress.SlotCount - 1) * gap;
            float startX = 0.5f - totalW * 0.5f;

            for (int s = 0; s < PlayerProgress.SlotCount; s++)
            {
                float x0 = startX + s * (cardW + gap);
                float x1 = x0 + cardW;
                BuildSlotCard(rt, s, new Vector2(x0, 0.09f), new Vector2(x1, 0.845f));
            }

            // Footer note
            var footGo = MakeRect(rt, "Footer",
                new Vector2(0.06f, 0.015f), new Vector2(0.94f, 0.08f));
            var foot = footGo.AddComponent<Text>();
            foot.font = _font; foot.fontSize = UIScale.FontSmall;
            foot.color = new Color(0.50f, 0.62f, 0.70f, 0.95f);
            foot.alignment = TextAnchor.MiddleCenter;
            foot.text = "Progress saves on this device  ·  Everything is earned by playing";
            foot.raycastTarget = false;
        }

        void BuildSlotCard(RectTransform parent, int slot, Vector2 aMin, Vector2 aMax)
        {
            bool exists = PlayerProgress.SlotExists(slot);
            var (name, highestLevel, currency) = PlayerProgress.GetSlotPreview(slot);

            // The border used to be a CHILD of the card, stretched 2px past it, drawn in cyan
            // at 75% alpha. In uGUI a parent's Image draws BEFORE its children, so that border
            // was painted over the entire card face -- which is why every card looked like a
            // bright cyan slab and the text on it was unreadable. It is a sibling now, created
            // first so the card face covers it and only the 3px rim shows.
            var bdrGo = MakeRect(parent, $"Slot{slot}Brd", aMin, aMax);
            var bdrRt = bdrGo.GetComponent<RectTransform>();
            bdrRt.offsetMin = new Vector2(-3, -3);
            bdrRt.offsetMax = new Vector2(3, 3);
            bdrGo.AddComponent<Image>().color = exists ? BorderExist : BorderEmpty;

            var cardGo = MakeRect(parent, $"Slot{slot}", aMin, aMax);
            var cardRt = cardGo.GetComponent<RectTransform>();

            // Card background
            var bgImg = cardGo.AddComponent<Image>();
            bgImg.color = exists ? CardBgExist : CardBgEmpty;

            // Header strip: the number and status sit on their own band
            var headGo = MakeRect(cardRt, "head",
                new Vector2(0f, 0.855f), new Vector2(1f, 1f));
            headGo.AddComponent<Image>().color = exists ? HeadExist : HeadEmpty;
            headGo.GetComponent<Image>().raycastTarget = false;

            // Slot number badge
            var numGo = MakeRect(cardRt, "num",
                new Vector2(0.0f, 0.86f), new Vector2(0.5f, 1.0f));
            var num = numGo.AddComponent<Text>();
            num.font = _font; num.fontSize = UIScale.FontH1; num.fontStyle = FontStyle.Bold;
            num.color = exists ? CyanPrimary : new Color(0.3f, 0.38f, 0.45f);
            num.alignment = TextAnchor.MiddleLeft;
            num.text = $"  {(slot + 1):D2}";
            num.raycastTarget = false;

            // Status tag
            var tagGo = MakeRect(cardRt, "tag",
                new Vector2(0.5f, 0.88f), new Vector2(1.0f, 0.99f));
            var tag = tagGo.AddComponent<Text>();
            tag.font = _font; tag.fontSize = UIScale.FontSmall; tag.fontStyle = FontStyle.Bold;
            tag.color = exists ? new Color(0.2f, 0.85f, 0.5f) : new Color(0.5f, 0.25f, 0.25f);
            tag.alignment = TextAnchor.MiddleRight;
            tag.text = exists ? "● SAVED  " : "○ EMPTY  ";
            tag.raycastTarget = false;

            // Divider under badge
            MakeLine(cardRt, new Vector2(0.0f, 0.855f), new Vector2(1.0f, 0.855f), 2,
                exists ? new Color(0f, 0.6f, 0.75f, 0.4f) : new Color(0.2f, 0.25f, 0.3f, 0.3f));

            if (exists)
            {
                BuildExistingSlotContent(cardRt, slot, name, highestLevel, currency);
            }
            else
            {
                BuildEmptySlotContent(cardRt, slot);
            }
        }

        void BuildExistingSlotContent(RectTransform card, int slot, string name, int highestLevel, int[] currency)
        {
            // Pilot name
            // The filled card used to leave a dead band between the badge and the name.
            // Everything now stacks from the top with no gap wider than one row.
            var nameGo = MakeRect(card, "name",
                new Vector2(0.04f, 0.735f), new Vector2(0.96f, 0.845f));
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.font = _font; nameTxt.fontSize = UIScale.FontH1; nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.supportRichText = false;
            nameTxt.color = Color.white;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            // One pilot name per player, shown on every card. Falls back to the slot's local
            // name until the player picks one.
            string shown = PlayerProgress.HasPilotName ? PlayerProgress.PilotName : name;
            nameTxt.text = shown;
            nameTxt.raycastTarget = false;

            // Editing the name claims it as a globally-unique handle via UsernameService
            // (Firestore-backed; falls back to a local-only name when offline).
            var editGo = MakeRect(card, "nameEdit", Vector2.zero, Vector2.one);
            var editRt = editGo.GetComponent<RectTransform>();
            editRt.anchorMin = new Vector2(0.04f, 0.735f);
            editRt.anchorMax = new Vector2(0.96f, 0.845f);
            editRt.offsetMin = editRt.offsetMax = Vector2.zero;
            var editImg = editGo.AddComponent<Image>();
            editImg.color = new Color(1f, 1f, 1f, 0.06f);          // a faint tappable well
            var field = editGo.AddComponent<InputField>();
            field.textComponent   = nameTxt;
            field.text            = shown;
            field.characterLimit  = UsernameService.MaxLen;
            field.lineType        = InputField.LineType.SingleLine;
            field.transition      = Selectable.Transition.None;
            nameGo.transform.SetParent(editGo.transform, false);
            var nRt = nameGo.GetComponent<RectTransform>();
            nRt.anchorMin = Vector2.zero; nRt.anchorMax = Vector2.one;
            nRt.offsetMin = new Vector2(6f, 0f); nRt.offsetMax = new Vector2(-6f, 0f);
            int capturedSlot = slot;
            field.onEndEdit.AddListener(v =>
            {
                string fallback = PlayerProgress.HasPilotName
                    ? PlayerProgress.PilotName : $"PILOT {capturedSlot + 1}";
                if (!UsernameService.IsValid(v))
                {
                    field.text = fallback;
                    SetStatus($"Name must be {UsernameService.MinLen}–{UsernameService.MaxLen} letters or digits.",
                              new Color(1f, 0.5f, 0.4f));
                    return;
                }
                SetStatus("checking name…", TextQuiet);
                UsernameService.TryClaim(v, (status, accepted) =>
                {
                    field.text = accepted.Length > 0 ? accepted : fallback;
                    switch (status)
                    {
                        case UsernameService.Status.Ok:
                            SetStatus("✓ name reserved", new Color(0.4f, 0.95f, 0.55f)); break;
                        case UsernameService.Status.Taken:
                            SetStatus("That name is taken — try another.", new Color(1f, 0.5f, 0.4f)); break;
                        case UsernameService.Status.Offline:
                            SetStatus("Saved — it'll be reserved once you're online.",
                                      new Color(1f, 0.72f, 0.18f)); break;
                        case UsernameService.Status.Invalid:
                            SetStatus($"Name must be {UsernameService.MinLen}–{UsernameService.MaxLen} characters.",
                                      new Color(1f, 0.5f, 0.4f)); break;
                        default:
                            SetStatus("Couldn't save the name — try again.", new Color(1f, 0.5f, 0.4f)); break;
                    }
                });
            });

            // Deployment tier
            var tierGo = MakeRect(card, "tier",
                new Vector2(0.04f, 0.655f), new Vector2(0.96f, 0.735f));
            var tier = tierGo.AddComponent<Text>();
            tier.font = _font; tier.fontSize = UIScale.FontBody; tier.fontStyle = FontStyle.Bold;
            tier.color = TextBody;
            tier.alignment = TextAnchor.MiddleCenter;
            tier.text = $"Level {highestLevel} · {LevelConfig.LevelName(highestLevel)}";
            tier.raycastTarget = false;

            // Currency reserves
            var resLblGo = MakeRect(card, "resLbl",
                new Vector2(0.04f, 0.575f), new Vector2(0.96f, 0.645f));
            var resLbl = resLblGo.AddComponent<Text>();
            resLbl.font = _font; resLbl.fontSize = UIScale.FontSmall;
            resLbl.color = TextQuiet;
            resLbl.alignment = TextAnchor.MiddleCenter;
            resLbl.text = "RESOURCES";
            resLbl.raycastTarget = false;

            // Currency: five SQUARE tiles, three over two.
            // Five columns in one row forced each cell to be tall and narrow -- a 20%-wide
            // column stretched over a fifth of the card, which is why the tiles read as
            // pillars with a gem floating at the top. Three-then-two lets every tile be
            // roughly square, so the gem sits in a box the same shape as the gem.
            if (currency != null)
            {
                int[] rowCount = { 3, 2 };
                float rowTop = 0.565f, rowH = 0.082f, rowGap = 0.012f;
                int k = 0;
                for (int r = 0; r < 2; r++)
                {
                    int cols = rowCount[r];
                    float cellW = 0.26f, gapX = 0.035f;
                    float span = cols * cellW + (cols - 1) * gapX;
                    float sx = 0.5f - span * 0.5f;
                    float y1 = rowTop - r * (rowH + rowGap);
                    float y0 = y1 - rowH;
                    for (int c = 0; c < cols && k < BoardModel.GemKindCount; c++, k++)
                    {
                        float x0 = sx + c * (cellW + gapX);
                        var gemGo = MakeRect(card, $"gem{k}",
                            new Vector2(x0, y0), new Vector2(x0 + cellW, y1));
                        var gemBg = gemGo.AddComponent<Image>();
                        gemBg.color = new Color(GemColor[k].r * 0.13f,
                                                GemColor[k].g * 0.13f,
                                                GemColor[k].b * 0.13f);

                        var iconGo = MakeRect(gemGo.GetComponent<RectTransform>(), "icon",
                            new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f));
                        var iconRtSq = iconGo.GetComponent<RectTransform>();
                        iconRtSq.pivot = new Vector2(0.5f, 0.5f);
                        iconRtSq.sizeDelta = new Vector2(26f, 26f);
                        var iconImg = iconGo.AddComponent<RawImage>();
                        iconImg.texture = NeonArt.Gem(k, NeonTheme.Active.GemStyle);
                        iconImg.raycastTarget = false;

                        var valGo = MakeRect(gemGo.GetComponent<RectTransform>(), "val",
                            new Vector2(0f, 0.02f), new Vector2(1f, 0.42f));
                        var val = valGo.AddComponent<Text>();
                        val.font = _font; val.fontSize = UIScale.FontSmall; val.fontStyle = FontStyle.Bold;
                        val.color = GemColor[k];
                        val.alignment = TextAnchor.MiddleCenter;
                        val.resizeTextForBestFit = true;
                        val.resizeTextMinSize = 10; val.resizeTextMaxSize = UIScale.FontBody;
                        val.text = currency[k].ToString();
                        val.raycastTarget = false;
                    }
                }
            }

            // Divider before buttons
            MakeLine(card, new Vector2(0.05f, 0.375f), new Vector2(0.95f, 0.375f), 1,
                new Color(0f, 0.62f, 0.78f, 0.5f));

            // JACK IN button (primary)
            BuildButton(_font, card, "JackIn",
                new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.355f),
                "PLAY", CyanPrimary, new Color(0f, 0.18f, 0.28f),
                () => SelectSlot(slot));

            // WIPE button (danger, small) — 0.03 gap above JACK IN bottom
            BuildButton(_font, card, "Wipe",
                new Vector2(0.18f, 0.045f), new Vector2(0.82f, 0.165f),
                "DELETE", new Color(0.9f, 0.25f, 0.2f), new Color(0.18f, 0.04f, 0.04f),
                () => ConfirmWipe(slot));
        }

        void BuildEmptySlotContent(RectTransform card, int slot)
        {
            var emptyGo = MakeRect(card, "empty",
                new Vector2(0.06f, 0.52f), new Vector2(0.94f, 0.84f));
            var empty = emptyGo.AddComponent<Text>();
            empty.font = _font; empty.fontSize = UIScale.FontBody;
            empty.color = new Color(0.3f, 0.38f, 0.45f);
            empty.alignment = TextAnchor.MiddleCenter;
            empty.text = "Empty slot\n\nStart a new game here.";
            empty.raycastTarget = false;

            MakeLine(card, new Vector2(0.05f, 0.47f), new Vector2(0.95f, 0.47f), 1,
                new Color(0.2f, 0.25f, 0.3f, 0.3f));

            // INITIALIZE button
            BuildButton(_font, card, "Init",
                new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.42f),
                "NEW GAME", MagentaAccent, new Color(0.18f, 0.03f, 0.14f),
                () => InitSlot(slot));
        }

        // ─────────────────────────────────── actions ──────────────────────

        void SelectSlot(int slot)
        {
            gameObject.SetActive(false);
            OnSlotSelected?.Invoke(slot);
        }

        void InitSlot(int slot)
        {
            PlayerProgress.NewSlot(slot);
            gameObject.SetActive(false);
            OnSlotSelected?.Invoke(slot);
        }

        /// <summary>DELETE used to wipe a finished save on a single tap, with no confirmation,
        /// from a button sitting directly under PLAY. One mis-tap lost everything. It now asks,
        /// names the profile it is about to erase, and defaults to the safe answer.</summary>
        void ConfirmWipe(int slot)
        {
            var (name, highestLevel, _) = PlayerProgress.GetSlotPreview(slot);
            var rt = GetComponent<RectTransform>();

            var scrimGo = MakeRect(rt, "WipeConfirm", Vector2.zero, Vector2.one);
            var scrim = scrimGo.AddComponent<Image>();
            scrim.color = new Color(0f, 0.01f, 0.02f, 0.88f);
            var scrimBtn = scrimGo.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(() => Destroy(scrimGo));   // tap outside = cancel
            scrimGo.transform.SetAsLastSibling();
            var scrimRt = scrimGo.GetComponent<RectTransform>();

            var cardGo = MakeRect(scrimRt, "card",
                new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.68f));
            var cardRt = cardGo.GetComponent<RectTransform>();
            var bdr = MakeRect(scrimRt, "brd",
                new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.68f));
            bdr.GetComponent<RectTransform>().offsetMin = new Vector2(-3, -3);
            bdr.GetComponent<RectTransform>().offsetMax = new Vector2(3, 3);
            bdr.transform.SetAsFirstSibling();
            bdr.AddComponent<Image>().color = new Color(0.9f, 0.25f, 0.2f, 0.9f);
            cardGo.AddComponent<Image>().color = new Color(0.07f, 0.05f, 0.06f, 1f);
            cardGo.AddComponent<Button>().transition = Selectable.Transition.None; // eat taps

            var tGo = MakeRect(cardRt, "t", new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.92f));
            var t = tGo.AddComponent<Text>();
            t.font = _font; t.fontSize = UIScale.FontH1; t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.45f, 0.38f);
            t.alignment = TextAnchor.MiddleCenter;
            t.text = "Delete this pilot?";
            t.raycastTarget = false;

            var bGo = MakeRect(cardRt, "b", new Vector2(0.06f, 0.40f), new Vector2(0.94f, 0.68f));
            var b = bGo.AddComponent<Text>();
            b.font = _font; b.fontSize = UIScale.FontBody;
            b.color = new Color(0.82f, 0.84f, 0.86f);
            b.alignment = TextAnchor.UpperCenter;
            b.text = $"{name} — Level {highestLevel}\n\nThis erases the save permanently.\nIt cannot be undone.";
            b.raycastTarget = false;

            BuildButton(_font, cardRt, "cancel",
                new Vector2(0.06f, 0.10f), new Vector2(0.48f, 0.32f),
                "KEEP", CyanPrimary, new Color(0f, 0.18f, 0.28f),
                () => Destroy(scrimGo));

            BuildButton(_font, cardRt, "confirm",
                new Vector2(0.52f, 0.10f), new Vector2(0.94f, 0.32f),
                "DELETE", new Color(0.9f, 0.25f, 0.2f), new Color(0.22f, 0.04f, 0.04f),
                () => { Destroy(scrimGo); WipeSlot(slot); });
        }

        void WipeSlot(int slot)
        {
            PlayerProgress.DeleteSlot(slot);
            // Rebuild the card (simplest: rebuild entire screen)
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            BuildGrid();
            BuildHeader();
            BuildSlotCards();
        }

        // ──────────────────────────────── entrance anim ───────────────────

        IEnumerator EntranceAnim()
        {
            // Reuse an existing CanvasGroup (the host adds one) — AddComponent returns null on a
            // duplicate, which crashed here. Only destroy the group if we actually created it.
            var cg = gameObject.GetComponent<CanvasGroup>();
            bool created = cg == null;
            if (created) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            float dur = 0.5f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                cg.alpha = t / dur;
                yield return null;
            }
            cg.alpha = 1f;
            if (created) Destroy(cg);
        }

        // ─────────────────────────────────── helpers ──────────────────────

        static Button BuildButton(Font font, RectTransform parent, string name,
            Vector2 aMin, Vector2 aMax, string label, Color textCol, Color bgCol, Action onClick)
        {
            var go = MakeRect(parent, name, aMin, aMax);
            var img = go.AddComponent<Image>();
            img.color = bgCol;

            // border
            var bdr = MakeRect(go.GetComponent<RectTransform>(), "bdr", Vector2.zero, Vector2.one);
            bdr.GetComponent<RectTransform>().offsetMin = new Vector2(-1, -1);
            bdr.GetComponent<RectTransform>().offsetMax = new Vector2(1, 1);
            bdr.transform.SetAsFirstSibling();
            bdr.AddComponent<Image>().color = new Color(textCol.r * 0.7f, textCol.g * 0.7f, textCol.b * 0.7f, 0.6f);

            var txt = MakeRect(go.GetComponent<RectTransform>(), "lbl", Vector2.zero, Vector2.one);
            var t = txt.AddComponent<Text>();
            t.font = font;
            t.fontSize = UIScale.FontH2; t.fontStyle = FontStyle.Bold;
            t.color = textCol; t.alignment = TextAnchor.MiddleCenter;
            t.text = label; t.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
            btn.targetGraphic   = img;
            btn.colors          = cb;
            btn.transition      = Selectable.Transition.ColorTint;
            btn.onClick.AddListener(() => onClick?.Invoke());
            go.AddComponent<ButtonFeel>();
            return btn;
        }

        static GameObject MakeRect(RectTransform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        static void MakeLine(RectTransform parent, Vector2 aMin, Vector2 aMax, float pixH, Color col)
        {
            var go = new GameObject("line");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0, pixH);
            var img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
        }
    }
}
