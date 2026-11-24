// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Replays;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Game.Replays;

namespace osu.Game.Rulesets.Taiko.Replays
{
    internal class TaikoFramedReplayInputHandler : FramedReplayInputHandler<TaikoReplayFrame>
    {
        public TaikoFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        private static readonly List<TaikoAction> no_actions = new List<TaikoAction>();

        private readonly ReplayState<TaikoAction> replayState = new ReplayState<TaikoAction>();

        protected override bool IsImportant(TaikoReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            replayState.PressedActions = CurrentFrame?.Actions ?? no_actions;
            inputs.Add(replayState);
        }
    }
}
