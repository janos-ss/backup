using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace SonarLintChecker
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
    [TextViewRole(PredefinedTextViewRoles.Analyzable)]
    internal sealed class SonarLintCheckerProvider : IViewTaggerProvider, ITableDataSource
    {
        internal readonly ITableManager ErrorTableManager;
        internal readonly ITextDocumentFactoryService TextDocumentFactoryService;

        const string _sonarLintDataSource = "SonarLint";

        private readonly List<SonarLintSinkManager> _managers = new List<SonarLintSinkManager>();
        private readonly Dictionary<string, SonarLintChecker> _sonarLintCheckers = new Dictionary<string, SonarLintChecker>();

        internal static SonarLintCheckerProvider Instance { get; private set; }

        [ImportingConstructor]
        internal SonarLintCheckerProvider([Import]ITableManagerProvider provider, [Import] ITextDocumentFactoryService textDocumentFactoryService)
        {
            this.ErrorTableManager = provider.GetTableManager(StandardTables.ErrorsTable);
            this.TextDocumentFactoryService = textDocumentFactoryService;

            this.ErrorTableManager.AddSource(this, StandardTableColumnDefinitions.DetailsExpander, 
                                                   StandardTableColumnDefinitions.ErrorSeverity, StandardTableColumnDefinitions.ErrorCode,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.BuildTool,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.ErrorCategory,
                                                   StandardTableColumnDefinitions.Text, StandardTableColumnDefinitions.DocumentName, StandardTableColumnDefinitions.Line, StandardTableColumnDefinitions.Column);

            SonarLintCheckerProvider.Instance = this;
        }

        /// <summary>
        /// Create a tagger that does spell checking on the view/buffer combination.
        /// </summary>
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            ITagger<T> tagger = null;

            // Only attempt to spell check on the view's edit buffer (and multiple views could have that buffer open simultaneously so
            // only create one instance of the spell checker.
            if ((buffer == textView.TextBuffer) && (typeof(T) == typeof(IErrorTag)))
            {
                var checker = buffer.Properties.GetOrCreateSingletonProperty(typeof(SonarLintChecker), () => new SonarLintChecker(this, textView, buffer));

                // This is a thin wrapper around the SpellChecker that can be disposed of without shutting down the SpellChecker
                // (unless it was the last tagger on the spell checker).
                tagger = new SonarLintCheckerTagger(checker) as ITagger<T>;
            }

            return tagger;
        }

        #region ITableDataSource members
        public string DisplayName
        {
            get
            {
                return "SonarLint Checker";
            }
        }

        public string Identifier
        {
            get
            {
                return _sonarLintDataSource;
            }
        }

        internal void UpdateErrors(string path, List<object> issues)
        {
            SonarLintChecker checker;
            if (this._sonarLintCheckers.TryGetValue(path, out checker))
            {
                checker.UpdateErrors(issues);
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
            return new SonarLintSinkManager(this, sink);
        }
        #endregion

        public void AddSinkManager(SonarLintSinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (_managers)
            {
                _managers.Add(manager);

                // Add the pre-existing spell checkers to the manager.
                foreach (var checker in _sonarLintCheckers.Values)
                {
                    manager.AddSonarLintChecker(checker);
                }
            }
        }

        public void RemoveSinkManager(SonarLintSinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (_managers)
            {
                _managers.Remove(manager);
            }
        }

        public void AddSonarLintChecker(SonarLintChecker checker)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding or removing the 1st/last tagger).
            lock (_managers)
            {
                _sonarLintCheckers.Add(checker.FilePath, checker);

                // Tell the preexisting managers about the new spell checker
                foreach (var manager in _managers)
                {
                    manager.AddSonarLintChecker(checker);
                }
            }
        }

        public void RemoveSonarLintChecker(SonarLintChecker checker)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding or removing the 1st/last tagger).
            lock (_managers)
            {
                _sonarLintCheckers.Remove(checker.FilePath);

                foreach (var manager in _managers)
                {
                    manager.RemoveSonarLintChecker(checker);
                }
            }
        }

        public void UpdateAllSinks()
        {
            lock (_managers)
            {
                foreach (var manager in _managers)
                {
                    manager.UpdateSink();
                }
            }
        }
    }
}
