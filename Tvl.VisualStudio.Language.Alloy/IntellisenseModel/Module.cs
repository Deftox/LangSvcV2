﻿namespace Tvl.VisualStudio.Language.Alloy.IntellisenseModel
{
    using System;

    internal class Module : Element
    {
        public override string Name
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override AlloyFile File
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool IsExternallyVisible
        {
            get
            {
                return true;
            }
        }
    }
}
