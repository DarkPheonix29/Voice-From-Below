// using UnityEngine;
// using UnityEngine.Playables;

// public class QteBridgePlayableBehaviour : PlayableBehaviour
// {
//     public QteSequence sequence;
//     public ActFiveChaseDirector director;

//     PlayableDirector _timelineDirector;
//     bool _started;

//     public override void OnBehaviourPlay(Playable playable, FrameData info)
//     {
//         if (_started) return;
//         _started = true;

//         _timelineDirector = playable.GetGraph().GetResolver() as PlayableDirector;
//         if (_timelineDirector) _timelineDirector.Pause();

//         if (QteManager.Instance && sequence != null)
//         {
//             QteManager.Instance.OnSequenceEnd += OnQteEnd;
//             QteManager.Instance.StartSequence(sequence);
//         }
//         else
//         {
//             if (_timelineDirector) _timelineDirector.Resume();
//         }
//     }

//     void OnQteEnd(QteResult result)
//     {
//         if (QteManager.Instance != null)
//             QteManager.Instance.OnSequenceEnd -= OnQteEnd;

//         if (director != null) director.OnQteSequenceEnd(result == QteResult.Success);
//         if (_timelineDirector) _timelineDirector.Resume();
//     }

//     public override void OnBehaviourPause(Playable playable, FrameData info)
//     {
//         if (QteManager.Instance != null)
//             QteManager.Instance.OnSequenceEnd -= OnQteEnd;
//     }
// }
