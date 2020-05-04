using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Kinematica.Editor
{
    internal partial class Builder : IDisposable
    {
        public Builder(Asset asset)
        {
            allocator = new BlobAllocator(-1);

            ref var binary = ref allocator.ConstructRoot<Binary>();

            this.binary = MemoryRef<Binary>.Create(ref binary);

            this.asset = asset;

            stringTable = StringTable.Create();

            segments = Segments.Create();

            rig = AnimationRig.Create(asset.DestinationAvatar);

            factories = new Dictionary<Avatar, AnimationSamplerFactory>();
        }

        public void Dispose()
        {
            allocator.Dispose();

            foreach (var factory in factories)
            {
                factory.Value.Dispose();
            }
        }

        public bool Build(string filePath)
        {
            if (!PrepareSamplerFactories())
            {
                return false;
            }

            ref Binary binary = ref Binary;

            binary.FileVersion = Binary.s_CodeVersion;
            binary.SampleRate = asset.SampleRate;
            binary.TimeHorizon = asset.TimeHorizon;

            BuildAnimationRig();
            BuildSegments();
            BuildTransforms();
            BuildTags();
            BuildMetrics();
            BuildFragments();
            BuildStringTable();

            VerifyIntegrity();

            BlobFile.WriteBlobAsset(allocator, ref binary, filePath);

            return true;
        }

        public static Builder Create(Asset asset)
        {
            return new Builder(asset);
        }

        public ref Binary Binary
        {
            get { return ref binary.Ref; }
        }

        void VerifyIntegrity()
        {
            ref Binary binary = ref Binary;

            int numIntervals = binary.numIntervals;
            int numSegments = binary.numSegments;
            int numCodeBooks = binary.numCodeBooks;

            // Verify reference integrity between segments, tags and intervals.

            var expectedTagIndex = 0;
            var expectedIntervalIndex = 0;

            for (int i = 0; i < numSegments; ++i)
            {
                var segment = binary.GetSegment(i);

                Assert.IsTrue(segment.tagIndex == expectedTagIndex);
                Assert.IsTrue(segment.intervalIndex == expectedIntervalIndex);

                for (int j = 0; j < segment.numTags; ++j)
                {
                    Assert.IsTrue(binary.GetTag(
                        segment.tagIndex + j).segmentIndex == i);
                }

                for (int j = 0; j < segment.numIntervals; ++j)
                {
                    Assert.IsTrue(binary.GetInterval(
                        segment.intervalIndex + j).segmentIndex == i);
                }

                expectedTagIndex += segment.numTags;
                expectedIntervalIndex += segment.numIntervals;
            }

            Assert.IsTrue(expectedTagIndex == binary.numTags);
            Assert.IsTrue(expectedIntervalIndex == binary.numIntervals);

            // Verify reference integrity between intervals and codebooks.

            for (int i = 0; i < numCodeBooks; ++i)
            {
                int numCodeBookIntervals =
                    binary.codeBooks[i].intervals.Length;

                for (int j = 0; j < numCodeBookIntervals; ++j)
                {
                    int intervalIndex =
                        binary.codeBooks[i].intervals[j];

                    Assert.IsTrue(
                        binary.intervals[intervalIndex].codeBookIndex == i);
                }
            }

            for (int i = 0; i < numIntervals; ++i)
            {
                var codeBookIndex =
                    binary.intervals[i].codeBookIndex;

                if (codeBookIndex != Binary.CodeBookIndex.Invalid)
                {
                    Assert.IsTrue(
                        binary.codeBooks[codeBookIndex].Contains(i));
                }
            }
        }

        bool PrepareSamplerFactories()
        {
            foreach (var avatar in asset.GetAvatars())
            {
                try
                {
                    var factory =
                        AnimationSamplerFactory.Create(
                            avatar, rig);
                    factories.Add(avatar, factory);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Animation sampler error for avatar {avatar.name} : {e.Message}");
                    return false;
                }
            }

            return true;
        }

        //
        // Asset
        //

        Asset asset;

        //
        // Intermediate representation
        //

        Segments segments;

        StringTable stringTable;

        AnimationRig rig;

        Dictionary<Avatar, AnimationSamplerFactory> factories;

        //
        // Final memory-ready representation
        //

        MemoryRef<Binary> binary;

        BlobAllocator allocator;
    }
}
