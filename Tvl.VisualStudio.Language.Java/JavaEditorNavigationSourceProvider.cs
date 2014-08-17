﻿namespace Tvl.VisualStudio.Language.Java
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Utilities;
    using Tvl.VisualStudio.Language.Parsing;
    using Tvl.VisualStudio.Text.Navigation;
    using IDispatcherGlyphService = Tvl.VisualStudio.Language.Intellisense.IDispatcherGlyphService;
    using IOutputWindowService = Tvl.VisualStudio.OutputWindow.Interfaces.IOutputWindowService;

    [Export(typeof(IEditorNavigationSourceProvider))]
    [ContentType(Constants.JavaContentType)]
    public sealed class JavaEditorNavigationSourceProvider : IEditorNavigationSourceProvider
    {
        [Import]
        public IBackgroundParserFactoryService BackgroundParserFactoryService
        {
            get;
            private set;
        }

        [Import]
        public IEditorNavigationTypeRegistryService EditorNavigationTypeRegistryService
        {
            get;
            private set;
        }

        [Import]
        public IDispatcherGlyphService GlyphService
        {
            get;
            private set;
        }

        [Import]
        public IOutputWindowService OutputWindowService
        {
            get;
            private set;
        }

        public IEditorNavigationSource TryCreateEditorNavigationSource(ITextBuffer textBuffer)
        {
            var backgroundParser = BackgroundParserFactoryService.GetBackgroundParser(textBuffer);
            if (backgroundParser == null)
                return null;

            return new JavaEditorNavigationSource(textBuffer, backgroundParser, this);
        }
    }
}
