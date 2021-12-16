namespace MCBurst
{
    using System.Collections.Generic;
    using Unity.Jobs;
    using UnityEngine;

    public class JobFrame : MonoBehaviour
    {
        static JobFrame instnace;
        static void Init()
        {
            if(instnace != null) return;
            instnace = new GameObject("[JobFrameWorker]").AddComponent<JobFrame>();
        }

        public struct Worker
        {
            public System.Action<Worker> onComplete;
            public JobHandle handle;
            public float timeStart, timeEnd;
            public int frames;
            public bool ended;
            public void End( float t ) { timeEnd = t; ended = true; }
            public void Frame() => frames ++ ;
            public new string ToString() => $"{frames} frames in {(timeEnd - timeStart).ToString("N4")} seconds";
        }

        public List<Worker> workers;

        public static void Await( ref JobHandle handle, System.Action<Worker> onComplete )
        {
            Init();

            Worker worker = new Worker
            {
                onComplete = onComplete,
                handle = handle,
                timeStart = Time.realtimeSinceStartup,
                frames = 0
            };

            instnace.workers.Add( worker );
        }

        private void Update()
        {
            foreach( var worker in workers )
            {
                worker.Frame();

                if( worker.ended || ! worker.handle.IsCompleted ) continue;

                // Tracing data ownership requires dependencies to complete before the control
                // thread can use them again. It is not enough to check JobHandle.IsCompleted.
                // You must call the method JobHandle.Complete to regain ownership of the
                // NativeContainer types to the control thread

                worker.handle.Complete();

                worker.End( Time.realtimeSinceStartup );
             
                worker.onComplete?.Invoke( worker );
            }
        }
    }
}