using System;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal partial class Asset : ScriptableObject
    {
        /// <summary>
        /// TaggedAnimationClip that corresponds to the supplied AnimationClip
        /// </summary>
        /// <param name=clip>AnimationClip to use</param>
        /// <returns>Returns the TaggedAnimationClip associated with the supplied AnimationClip</returns>
        public bool ContainsAnimationClip(AnimationClip clip)
        {
            return AnimationLibrary.Any((TaggedAnimationClip taggedClip) => { return taggedClip.AnimationClip == clip; });
        }

        /// <summary>
        /// Finds the TaggedAnimationClip that corresponds to the supplied AnimationClip
        /// </summary>
        /// <param name=clip>AnimationClip to use</param>
        /// <returns>Returns the TaggedAnimationClip associated with the supplied AnimationClip</returns>
        internal TaggedAnimationClip FindTaggedAnimationClip(AnimationClip clip)
        {
            return AnimationLibrary.FirstOrDefault((TaggedAnimationClip taggedClip) => { return taggedClip.AnimationClip == clip; });
        }

        /// <summary>
        /// Creates and adds a new TaggedAnimationClip in the asset from an AnimationClip
        /// </summary>
        /// <completionlist cref=""/>
        /// <param name=clip>AnimationClip to use. Must be an asset on disk</param>
        /// <exception cref="System.ArgumentNullException">Thrown if argument clip is null</exception>
        /// <exception cref="System.ArgumentException">Thrown if argument clip no an asset on disk</exception>
        /// <returns>Returns the TaggedAnimationClip created from the supplied AnimationClip</returns>
        internal TaggedAnimationClip AddAnimationClip(AnimationClip clip)
        {
            if (clip == null)
                throw new ArgumentNullException("clip");

            TaggedAnimationClip taggedAnimationClip = null;

            //Don't add existing AnimationClip to library
            if (AnimationLibrary.Any((TaggedAnimationClip taggedClip) => { return taggedClip.AnimationClip == clip; }))
                return null;

            try
            {
                taggedAnimationClip = TaggedAnimationClip.BuildFromClip(clip, this, ETagImportOption.Import);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException("argument \"clip\" must be an asset on disk");
            }

            Undo.RecordObject(this, string.Format("Add Animation Clip {0}", clip.name));
            AnimationLibrary.Add(taggedAnimationClip);
            taggedAnimationClip.DataChanged += MarkDirty;

            MarkDirty();
            return taggedAnimationClip;
        }

        /// <summary>
        /// Removes the TaggedAnimationClip that corresponds to the supplied AnimationClip from the asset
        /// </summary>
        /// <param name=clip>AnimationClip to use</param>
        /// <returns>true if the clip was found and removed, false otherwise</returns>
        public bool RemoveAnimationClip(AnimationClip clip)
        {
            if (clip == null)
                throw new ArgumentNullException("clip");

            Undo.RecordObject(this, string.Format("Remove Animation Clip {0}", clip.name));

            int removed = AnimationLibrary.RemoveAll((TaggedAnimationClip tagged) => { return tagged.AnimationClip == clip; });

            if (removed > 0)
            {
                MarkDirty();
            }

            return removed > 0;
        }

        /// <summary>
        /// Builds the binary for this asset
        /// </summary>
        public void Build()
        {
            string errors = null;
            if (!CanBuild(ref errors))
            {
                Debug.LogError(errors);
                return;
            }

            try
            {
                var builder = Builder.Create(this);

                try
                {
                    string assetPath = AssetDatabase.GetAssetPath(this);
                    string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    string filePath = Path.GetFullPath(Binary.GetBinaryFilePathFromAssetGuid(assetGuid));

                    var fileDirectory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(fileDirectory))
                    {
                        Directory.CreateDirectory(fileDirectory);
                    }

                    if (builder.Build(filePath))
                    {
                        builder.Binary.GenerateDebugDocument().Save(
                            Path.ChangeExtension(assetPath, ".debug.xml"));
                        MarkDirty(false);
                        m_Data.syncedWithBinary = true;
                    }
                }
                finally
                {
                    builder.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{e.Message} : {e.InnerException}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
