// using UnityEngine;
// using UnityEngine.Playables;

// [System.Serializable]
// public class QteBridgePlayableAsset : PlayableAsset
// {
//     public QteSequence sequence; // your QTE data asset
//     public ExposedReference<ActFiveChaseDirector> director;

//     public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
//     {
//         var playable = ScriptPlayable<QteBridgePlayableBehaviour>.Create(graph);
//         var beh = playable.GetBehaviour();
//         beh.sequence = sequence;
//         beh.director = director.Resolve(graph.GetResolver());
//         return playable;
//     }
// }
