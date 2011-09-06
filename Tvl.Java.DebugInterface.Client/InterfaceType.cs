﻿namespace Tvl.Java.DebugInterface.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Tvl.Java.DebugInterface.Types;
    using System.Diagnostics.Contracts;
    using System.Collections.ObjectModel;

    internal sealed class InterfaceType : ReferenceType, IInterfaceType
    {
        // cached data
        private InterfaceType[] _superInterfaces;

        internal InterfaceType(VirtualMachine virtualMachine, InterfaceId typeId)
            : base(virtualMachine, new TaggedReferenceTypeId(TypeTag.Interface, typeId))
        {
            Contract.Requires(virtualMachine != null);
        }

        public ReadOnlyCollection<IClassType> GetImplementors()
        {
            throw new NotImplementedException();
        }

        public ReadOnlyCollection<IInterfaceType> GetSubInterfaces()
        {
            throw new NotImplementedException();
        }

        public ReadOnlyCollection<IInterfaceType> GetSuperInterfaces()
        {
            if (_superInterfaces == null)
            {
                InterfaceId[] interfaceIds;
                DebugErrorHandler.ThrowOnFailure(VirtualMachine.ProtocolService.GetInterfaces(out interfaceIds, ReferenceTypeId));
                InterfaceType[] interfaces = Array.ConvertAll(interfaceIds, VirtualMachine.GetMirrorOf);
                _superInterfaces = interfaces;
            }

            return new ReadOnlyCollection<IInterfaceType>(_superInterfaces);
        }
    }
}
