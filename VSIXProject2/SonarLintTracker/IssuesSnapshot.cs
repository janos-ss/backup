using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using System.Collections.Generic;
using System.Globalization;

namespace SonarLintTracker
{
    class IssuesSnapshot : WpfTableEntriesSnapshotBase
    {
        private readonly string filePath;
        private readonly int versionNumber;

        // We're not using an immutable list here but we cannot modify the list in any way once we've published the snapshot.
        public readonly List<IssueMarker> IssueMarkers = new List<IssueMarker>();

        public IssuesSnapshot NextSnapshot;

        internal IssuesSnapshot(string filePath, int versionNumber)
        {
            this.filePath = filePath;
            this.versionNumber = versionNumber;
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
                return versionNumber;
            }
        }

        public override bool TryGetValue(int index, string columnName, out object content)
        {
            if ((index >= 0) && (index < this.IssueMarkers.Count))
            {
                if (columnName == StandardTableKeyNames.DocumentName)
                {
                    content = filePath;
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
