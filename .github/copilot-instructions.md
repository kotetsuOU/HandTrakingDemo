# Copilot Instructions

## プロジェクト ガイドライン
- The user wants PCV_Controller (PointCloudViewer) to provide an option allowing them to select between using the color directly from imported files (e.g. .ply) or customizing it completely via Inspector settings. This ensures flexibility in debugging and viewing data.
- The project documentation should be consolidated into README.md and Home.md, as Markdown files under the .github directory are difficult to access.
- When adding debugging features to this repository, prioritize implementations that minimize processing load.
- The user is working on adding an 'OcclusionMode 3' with a directional binning model (majority voting mechanism for 3 directions) and optimizations for loop-invariant code motion (e.g., pulling coordinate inverse squared magnitude computations out of the loop).

## Code Functionality
- Ensure that the RsDeviceEditor.cs script's PlaybackMode Open button correctly updates the selected bag file by including `serializedObject.ApplyModifiedProperties()` in the implementation.