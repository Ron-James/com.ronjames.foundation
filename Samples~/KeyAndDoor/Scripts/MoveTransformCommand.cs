using RonJames.Foundation.Commands;
using RonJames.Foundation.Values;
using UnityEngine;

namespace RonJames.Foundation.Samples.KeyAndDoor
{
    /// <summary>
    /// A custom Command, written the way every Command should be: configuration in fields, all
    /// running state in locals, every await passed the context's token, and progress reported.
    /// If the Run is Skipped or Stopped mid-move, the object simply stays where it got to.
    /// </summary>
    public class MoveTransformCommand : Command
    {
        [SerializeField]
        private VariableReference<Transform> target = new VariableReference<Transform>();

        [SerializeField]
        [Tooltip("World-space offset from where the target is when the Command starts.")]
        private Vector3 offset = Vector3.up;

        [SerializeField]
        private VariableReference<float> seconds = new VariableReference<float>(1f);

        public MoveTransformCommand()
        {
        }

        public MoveTransformCommand(Transform target, Vector3 offset, float seconds)
        {
            this.target = new VariableReference<Transform>(target);
            this.offset = offset;
            this.seconds = new VariableReference<float>(seconds);
        }

        public override async Awaitable Execute(CommandContext context)
        {
            Transform moving = target.Value;
            if (moving == null)
            {
                throw new System.InvalidOperationException("Move Transform has no target.");
            }

            Vector3 from = moving.position;
            Vector3 to = from + offset;
            float duration = Mathf.Max(0.0001f, seconds.Value);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = elapsed / duration;
                moving.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                context.ReportProgress(t);
                await Awaitable.NextFrameAsync(context.Token);
            }

            moving.position = to;
        }
    }
}
