using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace SonarLintTracker
{
    /// <summary>
    /// Factory for the <see cref="ITagger{T}"/>. There will be one instance of this class/VS session.
    /// 
    /// It is also the <see cref="ITableDataSource"/> that reports Sonar errors.
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class TaggerProvider : IViewTaggerProvider, ITableDataSource
    {
        internal readonly ITableManager ErrorTableManager;
        internal readonly ITextDocumentFactoryService TextDocumentFactoryService;

        private readonly List<SinkManager> managers = new List<SinkManager>();
        private readonly Dictionary<string, IssueTracker> trackers = new Dictionary<string, IssueTracker>();

        internal static TaggerProvider Instance { get; private set; }

        [ImportingConstructor]
        internal TaggerProvider([Import] ITableManagerProvider provider, [Import] ITextDocumentFactoryService textDocumentFactoryService)
        {
            this.ErrorTableManager = provider.GetTableManager(StandardTables.ErrorsTable);
            this.TextDocumentFactoryService = textDocumentFactoryService;

            this.ErrorTableManager.AddSource(this, StandardTableColumnDefinitions.DetailsExpander, 
                                                   StandardTableColumnDefinitions.ErrorSeverity, StandardTableColumnDefinitions.ErrorCode,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.BuildTool,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.ErrorCategory,
                                                   StandardTableColumnDefinitions.Text, StandardTableColumnDefinitions.DocumentName,
                                                   StandardTableColumnDefinitions.Line, StandardTableColumnDefinitions.Column,
                                                   StandardTableColumnDefinitions.ProjectName);

            TaggerProvider.Instance = this;
        }

        /// <summary>
        /// Create a tagger that will track Sonar issues on the view/buffer combination.
        /// </summary>
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            // Only attempt to track the view's edit buffer.
            // Multiple views could have that buffer open simultaneously, so only create one instance of the tracker.
            if ((buffer == textView.TextBuffer) && (typeof(T) == typeof(IErrorTag)))
            {
                ITextDocument document;
                if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
                {
                    var filePath = document.FilePath;

                    // TODO what happens if the file gets renamed?
                    // TODO perhaps we can listen for the file changing its name (ITextDocument.FileActionOccurred)

                    lock (trackers)
                    {
                        if (!trackers.ContainsKey(filePath))
                        {
                            var tracker = new IssueTracker(this, buffer, filePath);

                            // This is a thin wrapper around the IssueTracker that can be disposed of without shutting down the IssueTracker
                            // (unless it was the last tagger on the IssueTracker).
                            return new Tagger(tracker) as ITagger<T>;
                        }
                    }
                }

            }

            return null;
        }

        #region ITableDataSource members
        public string DisplayName
        {
            get
            {
                return "SonarLint";
            }
        }

        public string Identifier
        {
            get
            {
                return "SonarLint";
            }
        }

        public string SourceTypeIdentifier
        {
            get
            {
                return StandardTableDataSources.ErrorTableDataSource;
            }
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            // This method is called to each consumer interested in errors. In general, there will be only a single consumer (the error list tool window)
            // but it is always possible for 3rd parties to write code that will want to subscribe.
            return new SinkManager(this, sink);
        }
        #endregion

        internal void UpdateIssues(string path, List<object> issues)
        {
            IssueTracker tracker;
            if (this.trackers.TryGetValue(path, out tracker))
            {
                tracker.UpdateIssues(issues);
            }
        }

        public void AddSinkManager(SinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (managers)
            {
                managers.Add(manager);

                foreach (var tracker in trackers.Values)
                {
                    manager.AddFactory(tracker.Factory);
                }
            }
        }

        public void RemoveSinkManager(SinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (managers)
            {
                managers.Remove(manager);
            }
        }

        public void AddIssueTracker(IssueTracker tracker)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding the 1st tagger).
            lock (managers)
            {
                trackers.Add(tracker.FilePath, tracker);

                foreach (var manager in managers)
                {
                    manager.AddFactory(tracker.Factory);
                }
            }
        }

        public void RemoveIssueTracker(IssueTracker tracker)
        {
            // This call will always happen on the UI thread (it is a side-effect of removing the last tagger).
            lock (managers)
            {
                trackers.Remove(tracker.FilePath);

                foreach (var manager in managers)
                {
                    manager.RemoveFactory(tracker.Factory);
                }
            }
        }

        public void UpdateAllSinks()
        {
            lock (managers)
            {
                foreach (var manager in managers)
                {
                    manager.UpdateSink();
                }
            }
        }
    }
}
