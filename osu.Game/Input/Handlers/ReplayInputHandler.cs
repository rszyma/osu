// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Input.Handlers;
using osu.Framework.Input.StateChanges;
using osu.Framework.Input.StateChanges.Events;
using osu.Framework.Input.States;
using osu.Framework.Platform;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Input.Handlers
{
    public abstract class ReplayInputHandler : InputHandler
    {
        /// <summary>
        /// A function that converts coordinates from gamefield to screen space.
        /// </summary>
        public Func<Vector2, Vector2> GamefieldToScreenSpace { protected get; set; }

        /// <summary>
        /// Update the current frame based on an incoming time value.
        /// There are cases where we return a "must-use" time value that is different from the input.
        /// This is to ensure accurate playback of replay data.
        /// </summary>
        /// <param name="time">The time which we should use for finding the current frame.</param>
        /// <returns>The usable time value. If null, we should not advance time as we do not have enough data.</returns>
        public abstract double? SetFrameFromTime(double time);

        public override bool Initialize(GameHost host) => true;

        /// <remarks>
        /// Replay input handlers may apply the same instance in each frame with different <see cref="PressedActions"/>.
        /// </remarks>
        public class ReplayState<T> : IInput
            where T : struct
        {
            public List<T> PressedActions;

            private ReplayStateChangeEvent<T> changeEvent;

            public void Apply(InputState state, IInputStateChangeHandler handler)
            {
                if (!(state is RulesetInputManagerInputState<T> inputState))
                    throw new InvalidOperationException($"{nameof(ReplayState<>)} should only be applied to a {nameof(RulesetInputManagerInputState<>)}");

                // The event is handled before this returns, so it can be used again for the next frame.
                if (changeEvent == null || changeEvent.State != state)
                    changeEvent = new ReplayStateChangeEvent<T>(state, this, new List<T>(), new List<T>());

                var lastPressed = inputState.LastPressedActions;
                var released = changeEvent.ReleasedActions;
                var pressed = changeEvent.PressedActions;

                released.Clear();
                pressed.Clear();

                foreach (var action in lastPressed)
                {
                    if (!PressedActions.Contains(action) && !released.Contains(action))
                        released.Add(action);
                }

                foreach (var action in PressedActions)
                {
                    if (!lastPressed.Contains(action) && !pressed.Contains(action))
                        pressed.Add(action);
                }

                lastPressed.Clear();
                lastPressed.AddRange(PressedActions);

                inputState.LastReplayState = this;

                handler.HandleInputStateChange(changeEvent);
            }
        }

        public class ReplayStateChangeEvent<T> : InputStateChangeEvent
        {
            public readonly List<T> ReleasedActions;
            public readonly List<T> PressedActions;

            public ReplayStateChangeEvent(InputState state, IInput input, List<T> releasedActions, List<T> pressedActions)
                : base(state, input)
            {
                ReleasedActions = releasedActions;
                PressedActions = pressedActions;
            }
        }

        /// <summary>
        /// An <see cref="IInput"/> that is triggered when a frame containing replay statistics arrives.
        /// </summary>
        public class ReplayStatisticsFrameInput : IInput
        {
            /// <summary>
            /// The frame containing the statistics.
            /// </summary>
            public ReplayFrame Frame;

            private ReplayStatisticsFrameEvent frameEvent;

            public void Apply(InputState state, IInputStateChangeHandler handler)
            {
                if (frameEvent == null || frameEvent.State != state)
                    frameEvent = new ReplayStatisticsFrameEvent(state, this, Frame);
                else
                    frameEvent.Frame = Frame;

                handler.HandleInputStateChange(frameEvent);
            }
        }

        /// <summary>
        /// An <see cref="InputStateChangeEvent"/> that is triggered when a frame containing replay statistics arrives.
        /// </summary>
        public class ReplayStatisticsFrameEvent : InputStateChangeEvent
        {
            /// <summary>
            /// The frame containing the statistics.
            /// </summary>
            public ReplayFrame Frame { get; internal set; }

            public ReplayStatisticsFrameEvent(InputState state, IInput input, ReplayFrame frame)
                : base(state, input)
            {
                Frame = frame;
            }
        }
    }
}
