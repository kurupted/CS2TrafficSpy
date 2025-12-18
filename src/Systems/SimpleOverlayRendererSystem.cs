using Colossal.Mathematics;
using Game.Rendering;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficSpy.Systems
{
    // A simple system to render lines and curves on the map overlay.
    // Adapted from EmploymentTracker.
    public partial class SimpleOverlayRendererSystem : SystemBase
    {
        private OverlayRenderSystem m_OverlayRenderSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OverlayRenderSystem = World.GetExistingSystemManaged<OverlayRenderSystem>();
        }

        protected override void OnUpdate()
        {
            // This system doesn't need to do anything in OnUpdate 
            // because it just passes buffers to the game's native overlay system.
        }

        public Buffer GetBuffer(out JobHandle dependencies)
        {
            return new Buffer(m_OverlayRenderSystem.GetBuffer(out dependencies));
        }

        public void AddBufferWriter(JobHandle handle)
        {
            m_OverlayRenderSystem.AddBufferWriter(handle);
        }

        public struct Buffer
        {
            private OverlayRenderSystem.Buffer m_Buffer;

            public Buffer(OverlayRenderSystem.Buffer buffer)
            {
                m_Buffer = buffer;
            }

            public void DrawCurve(Color color, Bezier4x3 curve, float width, float2 roundness)
            {
                m_Buffer.DrawCurve(color, curve, width, roundness);
            }

            public void DrawLine(Color color, Line3.Segment line, float width)
            {
                m_Buffer.DrawLine(color, line, width);
            }
        }
    }
}