// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    public partial class ArgonAccuracyCounter : GameplayAccuracyCounter, ISerialisableDrawable
    {
        protected override double RollingDuration => 250;

        [SettingSource("Wireframe opacity", "Controls the opacity of the wireframes behind the digits.")]
        public BindableFloat WireframeOpacity { get; } = new BindableFloat(0.25f)
        {
            Precision = 0.01f,
            MinValue = 0,
            MaxValue = 1,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel))]
        public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        public bool UsesFixedAnchor { get; set; }

        // The text component splits the text at '.', as LocalisableString.ToString() formats with the invariant culture.
        protected override bool TryFormatCount(double count, Span<char> destination, out int charsWritten)
            => count.FloorToDecimalDigits(4).TryFormat(destination, out charsWritten, "0.00%", CultureInfo.InvariantCulture);

        protected override IHasText CreateText() => new ArgonAccuracyTextComponent
        {
            WireframeOpacity = { BindTarget = WireframeOpacity },
            ShowLabel = { BindTarget = ShowLabel },
        };

        private partial class ArgonAccuracyTextComponent : CompositeDrawable, IHasText
        {
            private readonly ArgonCounterTextComponent wholePart;
            private readonly ArgonCounterTextComponent fractionPart;
            private readonly ArgonCounterTextComponent percentText;

            public IBindable<float> WireframeOpacity { get; } = new BindableFloat();

            public Bindable<bool> ShowLabel { get; } = new BindableBool();

            public LocalisableString Text
            {
                get => wholePart.Text;
                set => SetText(value.ToString());
            }

            public void SetText(ReadOnlySpan<char> text)
            {
                int percent = text.IndexOf('%');
                if (percent >= 0)
                    text = text[..percent];

                int dot = text.IndexOf('.');
                if (dot < 0)
                    dot = text.Length;

                wholePart.SetText(text[..dot]);

                // The fraction part shows its dot.
                Span<char> fraction = stackalloc char[Math.Max(1, text.Length - dot)];
                fraction[0] = '.';
                text[Math.Min(dot + 1, text.Length)..].CopyTo(fraction[1..]);
                fractionPart.SetText(fraction);
            }

            public ArgonAccuracyTextComponent()
            {
                AutoSizeAxes = Axes.Both;

                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Child = wholePart = new ArgonCounterTextComponent(Anchor.TopRight, BeatmapsetsStrings.ShowScoreboardHeadersAccuracy.ToUpper())
                            {
                                WireframeOpacity = { BindTarget = WireframeOpacity },
                                WireframeTemplate = @"###",
                                ShowLabel = { BindTarget = ShowLabel },
                            }
                        },
                        fractionPart = new ArgonCounterTextComponent(Anchor.TopLeft)
                        {
                            WireframeOpacity = { BindTarget = WireframeOpacity },
                            WireframeTemplate = @".##",
                            Scale = new Vector2(0.5f),
                        },
                        percentText = new ArgonCounterTextComponent(Anchor.TopLeft)
                        {
                            Text = @"%",
                            WireframeTemplate = @"#",
                            WireframeOpacity = { BindTarget = WireframeOpacity }
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ShowLabel.BindValueChanged(s =>
                {
                    fractionPart.Margin = new MarginPadding { Top = s.NewValue ? 12f * 2f + 4f : 4f }; // +4 to account for the extra spaces above the digits.
                    percentText.Margin = new MarginPadding { Top = s.NewValue ? 12f : 0 };
                }, true);
            }
        }
    }
}
