using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace SonarLintTracker
{
    class ErrorsSnapshot : WpfTableEntriesSnapshotBase
    {
        private readonly string _filePath;
        private readonly int _versionNumber;

        // We're not using an immutable list here but we cannot modify the list in any way once we've published the snapshot.
        public readonly List<IssueMarker> IssueMarkers = new List<IssueMarker>();

        public ErrorsSnapshot NextSnapshot;

        internal ErrorsSnapshot(string filePath, int versionNumber)
        {
            _filePath = filePath;
            _versionNumber = versionNumber;
        }

        public override int Count
        {
            get
            {
                return this.IssueMarkers.Count;
            }
        }

        public override int VersionNumber
        {
            get
            {
                return _versionNumber;
            }
        }

        public override int IndexOf(int currentIndex, ITableEntriesSnapshot newerSnapshot)
        {
            // This and TranslateTo() are used to map errors from one snapshot to a different one (that way the error list can do things like maintain the selection on an error
            // even when the snapshot containing the error is replaced by a new one).
            //
            // You only need to implement Identity() or TranslateTo() and, of the two, TranslateTo() is more efficient for the error list to use.

            // Map currentIndex to the corresponding index in newerSnapshot (and keep doing it until either
            // we run out of snapshots, we reach newerSnapshot, or the index can no longer be mapped forward).
            var currentSnapshot = this;            
            do
            {
                Debug.Assert(currentIndex >= 0);
                Debug.Assert(currentIndex < currentSnapshot.Count);

                currentIndex = currentSnapshot.IssueMarkers[currentIndex].NextIndex;

                currentSnapshot = currentSnapshot.NextSnapshot;
            }
            while ((currentSnapshot != null) && (currentSnapshot != newerSnapshot) && (currentIndex >= 0));

            return currentIndex;
        }

        public override bool TryGetValue(int index, string columnName, out object content)
        {
            if ((index >= 0) && (index < this.IssueMarkers.Count))
            {
                if (columnName == StandardTableKeyNames.DocumentName)
                {
                    content = _filePath;
                    return true;
                }
                else if (columnName == StandardTableKeyNames.ErrorCategory)
                {
                    // TODO issue category, such as Sonar Bug, Sonar Code Smell
                    content = "Sonar ???";
                    return true;
                }
                else if (columnName == StandardTableKeyNames.ErrorSource)
                {
                    // TODO ok that it's not IntelliSense like for SonarC# ?
                    content = "SonarLint";
                    return true;
                }
                else if (columnName == StandardTableKeyNames.Line)
                {
                    content = this.IssueMarkers[index].Span.Start.GetContainingLine().LineNumber;
                    return true;
                }
                else if (columnName == StandardTableKeyNames.Column)
                {
                    var position = this.IssueMarkers[index].Span.Start;
                    var line = position.GetContainingLine();
                    content = position.Position - line.Start.Position;
                    return true;
                }
                else if (columnName == StandardTableKeyNames.Text)
                {
                    // TODO issue name
                    content = string.Format(CultureInfo.InvariantCulture, "SonarLint: {0}", this.IssueMarkers[index].Span.GetText());
                    return true;
                }
                else if (columnName == StandardTableKeyNames.ErrorSeverity)
                {
                    content = __VSERRORCATEGORY.EC_WARNING;
                    return true;
                }
                else if (columnName == StandardTableKeyNames.BuildTool)
                {
                    // TODO for example SonarAnalyzer.CSharp [SonarLint for Visual Studio 2015]
                    content = "SonarAnalyzer.?";
                    return true;
                }
                else if (columnName == StandardTableKeyNames.ErrorCode)
                {
                    // TODO
                    content = "S????";
                    return true;
                }
                else if ((columnName == StandardTableKeyNames.ErrorCodeToolTip) || (columnName == StandardTableKeyNames.HelpLink))
                {
                    // TODO see how SL for C# does this
                    content = string.Format(CultureInfo.InvariantCulture, "http://www.sonarlint.org/visualstudio/rules/index.html#version=5.9.0.992&ruleId={0}", this.IssueMarkers[index].Span.GetText());
                    return true;
                }
                else if (columnName == StandardTableKeyNames.ProjectName)
                {
                    // TODO
                }
                else if (columnName == StandardTableKeyNames.ProjectGuid)
                {
                    // TODO : not sure if this is needed. Maybe ok to omit as long as the Project column is visible?
                }
            }

            content = null;
            return false;
        }

        public override bool CanCreateDetailsContent(int index)
        {
            return true;
        }

        public override bool TryCreateDetailsStringContent(int index, out string content)
        {
            // TODO
            content = "Using the readonly keyword on a field means that ...";
            return (content != null);
        }
    }
}
