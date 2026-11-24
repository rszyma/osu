// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Osu.Replays
{
    public class OsuFramedReplayInputHandler : FramedReplayInputHandler<OsuReplayFrame>
    {
        public OsuFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        private static readonly List<OsuAction> no_actions = new List<OsuAction>();

        private readonly MousePositionAbsoluteInput positionInput = new MousePositionAbsoluteInput();
        private readonly ReplayState<OsuAction> replayState = new ReplayState<OsuAction>();

        protected override bool IsImportant(OsuReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            var position = Interpolation.ValueAt(CurrentTime, StartFrame.Position, EndFrame.Position, StartFrame.Time, EndFrame.Time);

            positionInput.Position = GamefieldToScreenSpace(position);
            inputs.Add(positionInput);

            replayState.PressedActions = CurrentFrame?.Actions ?? no_actions;
            inputs.Add(replayState);
        }
    }
}
