using DG.Tweening;
using UnityEngine;

namespace Zayats.Unity.View
{
    public class AnimationStart : MonoBehaviour
    {
        private ViewContext _view;
        
        public void Initialize(ViewContext view)
        {
            _view = view;
        }

        void Update()
        {
            var seqs = _view.AnimationSequences;
            if (seqs.Count > 0)
            {
                var s = seqs.First.Value;
                if (!s.IsPlaying())
                    s.Play();
            }
        }
    }
}