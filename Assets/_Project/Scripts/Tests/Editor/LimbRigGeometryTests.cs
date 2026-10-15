using NUnit.Framework;
using NW.App;
using UnityEngine;

namespace NW.Tests
{
    public class LimbRigGeometryTests
    {
        GameObject root;
        RectTransform Host(Vector2 size, bool stretch = false)
        {
            root = new GameObject("rig-test", typeof(RectTransform));
            var parent = (RectTransform)root.transform;
            parent.sizeDelta = size;
            if (!stretch) return parent;
            var child = new GameObject("stretch", typeof(RectTransform));
            var rt = (RectTransform)child.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }
        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        [TestCase("cybtrooper", false)] [TestCase("cybtitan", false)]
        [TestCase("kami", false)] [TestCase("synracer", false)]
        [TestCase("cybtrooper", true)]
        public void NeutralLayersShareTheBodyRectangle(string id, bool stretch)
        {
            var host = Host(new Vector2(256, 256), stretch);
            var limbs = LimbRigView.Build(host, id, true);
            var expected = new Vector3[4]; host.GetWorldCorners(expected);
            foreach (var limb in limbs)
            {
                var actual = new Vector3[4]; limb.rectTransform.GetWorldCorners(actual);
                for (int c = 0; c < 4; c++)
                    Assert.That(Vector3.Distance(actual[c], expected[c]), Is.LessThan(0.001f),
                        $"{id}/{limb.name} corner {c} must register with torso");
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void KneeRemainsAttachedThroughoutHipAndKneeRotation(bool mirror)
        {
            var host = Host(new Vector2(256, 256));
            var limbs = LimbRigView.Build(host, "cybtrooper", true);
            for (int i = 0; i < limbs.Length; i++)
            {
                var info = NeonArt.LimbInfo("cybtrooper", i);
                var parent = info.Parent < 0 ? host : limbs[info.Parent].rectTransform;
                LimbRigView.ConfigureSegment(limbs[i].rectTransform, parent, host.rect.size, info.Joint, mirror);
            }
            for (int frame = 0; frame < 32; frame++)
            {
                LimbRigView.Drive(limbs, frame * Mathf.PI / 16, !mirror, "cybtrooper");
                foreach (int shin in new[] { 1, 3 })
                {
                    var info = NeonArt.LimbInfo("cybtrooper", shin);
                    var parent = limbs[info.Parent].rectTransform;
                    var joint = info.Joint;
                    if (mirror) joint.x = 1 - joint.x;
                    Vector2 local = Vector2.Scale(joint - parent.pivot, parent.rect.size);
                    var expected = parent.TransformPoint(local);
                    Assert.That(Vector3.Distance(expected, limbs[shin].transform.position), Is.LessThan(.001f));
                }
            }
        }

        [Test]
        public void MirroredRootPivotsCoincideWithMirroredArtwork()
        {
            var host = Host(new Vector2(256, 256));
            var segment = new GameObject("segment", typeof(RectTransform)).GetComponent<RectTransform>();
            LimbRigView.ConfigureSegment(segment, host, host.rect.size, new Vector2(.2f, .7f), true);
            Assert.That(Vector3.Distance(segment.position, host.TransformPoint(new Vector3(76.8f, 51.2f))), Is.LessThan(.001f));
        }
    }
}
