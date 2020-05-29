# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/ )
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html ).

## [0.6.0-preview] - 2020-05-29

### Fixed
- Fixed inaccurate animation clip length computation when clip was over 5 minutes long
- Fixed memory corruption potentially happening if a memory identifier is invalid
- Fixed NavigationTask desired trajectory not cleared when goal was reached
- Fixed crash in Task Graph

### Added
- Added function to MotionSynthesizer to check a memory identifier is valid and prevent errors in client code
- Added option to create TrajectoryPrediction with provided current velocity

### Changed
- Renamed SteerRootDeltaTransfrom into SteerRootMotion and expose intermediate function in API
- Exposed MotionSynthesizer.CurrentVelocity in API

## [0.5.0-preview.1] - 2020-05-05

### Fixed
- Fixed timeline inspectors not updating when manipulating tags in timeline
- Fixed warning in InputUtility
- Fixed issue where Boundary clips could not be cleared
- Fixed issue where small tags would be moved when selected
- Fixed an issue where using the AutoFrame (A) key would have inconsistent behaviour
- Fixed an issue where undo/redo was not correctly updating preview
- Fixed an issue where deleting a previewed game object or creating new scene during preview would cause console error spam
- Fixed missing label in Annotation property drawers
- Fixed possible event leaks relating to Marker manipulation guidelines
- Fixed an issue where some debug information in the task graph and in-scene weren't showing up properly on HDRP
- Fixed ReflectionTypeLoadException exception occuring when using Microsoft Analysis assembly
- Fixed exception thrown in debug frame info when synthesizer was invalid
- Fixed TrajectoryFragment node debug display which were displaying wrong information and throwing exception
- Fixed wrong SequenceTask documentation
- Fixed tag manipulation so users can re-order and change tag time in the same operation
- Fixed Metric track updating during undo/redo and when changing boundary clips
- Fixed constant repaint of the snapshot debugger window to only occur during play mode
- Fixed guideline flickering when creating tags while previewing
- Fixed the GameObject icon in the timeline when using the personal skin
- Fixed crash in Navigation sample happening after second click in game view
- Fixed tag detection in builder which were too lenient. Among other things, it was assuming boundary clips duration was as long as time horizon

### Added
- Added vertical guidelines and time labels when manipulating tags and markers
- Added new dropdown control to choose in-scene preview target
- Added sample showcasing Scene manipulators for manipulating position and rotation fields in annotations
- Added current clip name and frame debug information to TaskGraph TimeIndex nodes.
- Added mouse hover highlighting to Preview Selector element clarifying how it can be interacted with
- Added a Scene Hierarchy ping to the current Preview Target
- Added explicit loop and reset when executed options to SequenceTask to make it more intuitive
- Added "none" option to the preview/debug target dropdown
- Added readmes to samples

### Changed
- Changed the layout of the Builder Window to improve the use of space available and hideable elements
- Changed the styling of tags and markers in the Timeline view
- Changed the behaviour of the animation preview to behave more similarly to Unity Timeline
- Changed preview activation conditions. Preview will now turn on automatically if the playhead is moved and a valid target is selected
- Re-enabled animation frame debugger
- Updated dependency on com.unity.collections to 0.5.1-preview.11
- Updated dependency on com.unity.jobs to 0.2.4-preview.11
- Updated dependency on com.unity.burst to 1.2.3
- Gutter track toggles are now in a dropdown menu
- Active time field is smaller
- Samples given more unified look and some experience improvements
- Snapshot debugger's timeline now supports alt+LMB to pan the time area
- DebugDraw.Begin/End are now public

## [0.4.0-preview] - 2020-03-19
### This is the first release of *unity.com.kinematica*.
 - Kinematica Asset authoring - tagging, markers, metrics
 - Kinematica Runtime - motion matching, debugging, task graph, retargeting (Unity 2020.1+)
 - Snapshot Debugger - data agnostic debugging and playback, used by kinematic to provide debugging support.
