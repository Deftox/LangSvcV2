﻿namespace Tvl.VisualStudio.Language.Java.Debugger
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Debugger.Interop;
    using Tvl.Java.DebugInterface;
    using System.Collections.ObjectModel;
    using System.Collections.Generic;
    using Tvl.VisualStudio.Language.Java.Debugger.Collections;
    using Tvl.Java.DebugInterface.Request;

    [ComVisible(true)]
    public class JavaDebugThread
        : IDebugThread3
        , IDebugThread2
        , IDebugQueryEngine2
        , IDebugThread90
        , IDebugThread100
    {
        private readonly JavaDebugProgram _program;
        private readonly IThreadReference _thread;
        private readonly ThreadCategory _category;

        private readonly List<IStepRequest> _stepRequests = new List<IStepRequest>();

        public JavaDebugThread(JavaDebugProgram program, IThreadReference thread, ThreadCategory category)
        {
            Contract.Requires<ArgumentNullException>(program != null, "program");
            Contract.Requires<ArgumentNullException>(thread != null, "thread");

            _program = program;
            _thread = thread;
            _category = category;
            CreateStepRequests();
        }

        public JavaDebugProgram Program
        {
            get
            {
                return _program;
            }
        }

        public ReadOnlyCollection<IStepRequest> StepRequests
        {
            get
            {
                return _stepRequests.AsReadOnly();
            }
        }

        public IStepRequest GetStepRequest(StepSize size, StepDepth depth)
        {
            int kindIndex;
            switch (depth)
            {
            case StepDepth.Into:
                kindIndex = 0;
                break;

            case StepDepth.Out:
                kindIndex = 1;
                break;

            case StepDepth.Over:
                kindIndex = 2;
                break;

            default:
                throw new NotSupportedException();
            }

            int unitIndex;
            switch (size)
            {
            case StepSize.Instruction:
                unitIndex = 0;
                break;

            case StepSize.Line:
                unitIndex = 1;
                break;

            case StepSize.Statement:
                unitIndex = 2;
                break;

            default:
                throw new NotSupportedException();
            }

            IStepRequest request = _stepRequests[kindIndex * 3 + unitIndex];
            Contract.Assert(request.Size == size);
            Contract.Assert(request.Depth == depth);
            return request;
        }

        private void CreateStepRequests()
        {
            IVirtualMachine virtualMachine = _thread.GetVirtualMachine();
            IEventRequestManager manager = virtualMachine.GetEventRequestManager();

            /*
             * THE ORDER OF THESE MUST MATCH THE LOOKUP IN GetStepRequest!
             */

            // step into
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Instruction, StepDepth.Into));
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Line, StepDepth.Into));
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Statement, StepDepth.Into));

            // step out
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Instruction, StepDepth.Out));
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Line, StepDepth.Out));
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Statement, StepDepth.Out));

            // step over
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Instruction, StepDepth.Over));
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Line, StepDepth.Over));
            _stepRequests.Add(manager.CreateStepRequest(_thread, StepSize.Statement, StepDepth.Over));

            foreach (var request in _stepRequests)
            {
                request.SuspendPolicy = SuspendPolicy.All;
            }
        }

        #region IDebugThread2 Members

        public int CanSetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            JavaDebugStackFrame stackFrame = pStackFrame as JavaDebugStackFrame;
            JavaDebugCodeContext codeContext = pCodeContext as JavaDebugCodeContext;
            if (stackFrame == null || codeContext == null)
                return VSConstants.E_INVALIDARG;

            // TODO: implement Set Next Statement
            return VSConstants.S_FALSE;
        }

        public int EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 ppEnum)
        {
            ppEnum = null;

            ReadOnlyCollection<IStackFrame> stackFrames = _thread.GetFrames();
            List<FRAMEINFO> frames = new List<FRAMEINFO>();

            FRAMEINFO[] frameInfo = new FRAMEINFO[1];
            foreach (var stackFrame in stackFrames)
            {
                JavaDebugStackFrame javaStackFrame = new JavaDebugStackFrame(this, stackFrame);
                int result = javaStackFrame.GetInfo(dwFieldSpec, nRadix, frameInfo);
                if (!ErrorHandler.Succeeded(result))
                    return result;

                frames.Add(frameInfo[0]);
            }

            ppEnum = new EnumDebugFrameInfo(frames);
            return VSConstants.S_OK;
        }

        public int GetName(out string pbstrName)
        {
            pbstrName = _thread.GetName();
            return VSConstants.S_OK;
        }

        public int GetProgram(out IDebugProgram2 ppProgram)
        {
            ppProgram = _program;
            return VSConstants.S_OK;
        }

        public int GetThreadId(out uint pdwThreadId)
        {
            pdwThreadId = (uint)_thread.GetUniqueId();
            return VSConstants.S_OK;
        }

        public int GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] ptp)
        {
            if (ptp == null)
                throw new ArgumentNullException("ptp");
            if (ptp.Length == 0)
                throw new ArgumentException();

            ptp[0].dwFields = 0;

            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_ID) != 0)
            {
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_ID;
                ptp[0].dwThreadId = (uint)_thread.GetUniqueId();
            }

            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_LOCATION) != 0)
            {
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_LOCATION;
                ptp[0].bstrLocation = "Unknown";
            }

            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_NAME) != 0)
            {
                if (ErrorHandler.Succeeded(GetName(out ptp[0].bstrName)))
                    ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_NAME;
            }

            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_PRIORITY) != 0)
            {
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_PRIORITY;
                ptp[0].bstrPriority = "Unknown";
            }

            ThreadStatus status = _thread.GetStatus();
            int suspendCount = status != ThreadStatus.Zombie ? _thread.GetSuspendCount() : 0;

            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_STATE) != 0)
            {
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_STATE;

                enum_THREADSTATE vsthreadState = 0;
                if (suspendCount > 1)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_FROZEN;
                else if (status == ThreadStatus.Zombie)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_DEAD;
                else if (status == ThreadStatus.Sleeping || status == ThreadStatus.Wait || status == ThreadStatus.Monitor)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_STOPPED;
                else if (status == ThreadStatus.NotStarted)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_FRESH;
                else
                    vsthreadState = enum_THREADSTATE.THREADSTATE_RUNNING;

                ptp[0].dwThreadState = (uint)vsthreadState;
            }

            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_SUSPENDCOUNT) != 0)
            {
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_SUSPENDCOUNT;
                ptp[0].dwSuspendCount = (uint)suspendCount;
            }

            return VSConstants.S_OK;
        }

        public int Resume(out uint pdwSuspendCount)
        {
            _thread.Resume();
            pdwSuspendCount = (uint)_thread.GetSuspendCount();
            return VSConstants.S_OK;
        }

        public int SetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            throw new NotImplementedException();
        }

        public int SetThreadName(string pszName)
        {
            throw new NotImplementedException();
        }

        public int Suspend(out uint pdwSuspendCount)
        {
            _thread.Suspend();
            pdwSuspendCount = (uint)_thread.GetSuspendCount();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Debug engines do not implement this method.
        /// </summary>
        /// <param name="pStackFrame">An IDebugStackFrame2 object that represents the stack frame.</param>
        /// <param name="ppLogicalThread">
        /// Returns an IDebugLogicalThread2 interface that represents the associated logical thread.
        /// A debug engine implementation should set this to a null value.
        /// </param>
        /// <returns>Debug engine implementations always return E_NOTIMPL.</returns>
        int IDebugThread2.GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        {
            ppLogicalThread = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread3.GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        {
            ppLogicalThread = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        // these aren't documented...
        #region IDebugThread3 Members

        public int CanRemapLeafFrame()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int IsCurrentException()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int RemapLeafFrame()
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugThread90 Members

        public int GetThreadProperties90(uint dwFields, THREADPROPERTIES90[] ptp)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDebugThread100 Members

        public int CanDoFuncEval()
        {
            throw new NotImplementedException();
        }

        public int GetFlags(out uint pFlags)
        {
            throw new NotImplementedException();
        }

        public int SetFlags(uint flags)
        {
            throw new NotImplementedException();
        }

        public int GetThreadDisplayName(out string bstrDisplayName)
        {
            throw new NotImplementedException();
        }

        public int GetThreadProperties100(uint dwFields, THREADPROPERTIES100[] ptp)
        {
            enum_THREADPROPERTY_FIELDS100 fields = (enum_THREADPROPERTY_FIELDS100)dwFields;

            if (ptp == null)
                throw new ArgumentNullException("ptp");
            if (ptp.Length == 0)
                throw new ArgumentException();

            ptp[0].dwFields = 0;

            // thread ID
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_ID) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_ID;
                ptp[0].dwThreadId = (uint)_thread.GetUniqueId();
            }

            // thread location
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_LOCATION) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_LOCATION;
                ptp[0].bstrLocation = "Unknown";
            }

            // name
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_NAME) != 0)
            {
                if (ErrorHandler.Succeeded(GetName(out ptp[0].bstrName)))
                    ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_NAME;
            }

            // display name
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME) != 0)
            {
                if (ErrorHandler.Succeeded(GetName(out ptp[0].bstrDisplayName)))
                    ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME;
            }

            // display name priority
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME_PRIORITY) != 0)
            {
                ptp[0].DisplayNamePriority = (uint)DISPLAY_NAME_PRIORITY100.DISPLAY_NAME_PRI_NORMAL_100;
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME_PRIORITY;
            }

            // thread priority (string)
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_PRIORITY) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_PRIORITY;
                ptp[0].bstrPriority = "Unknown";
            }

            // thread priority (id)
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_PRIORITY_ID) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_PRIORITY_ID;
                ptp[0].priorityId = 0;
            }


            ThreadStatus status = _thread.GetStatus();
            int suspendCount = status != ThreadStatus.Zombie ? _thread.GetSuspendCount() : 0;

            // thread state
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_STATE) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_STATE;

                enum_THREADSTATE vsthreadState = 0;
                if (suspendCount > 1)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_FROZEN;
                else if (status == ThreadStatus.Zombie)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_DEAD;
                else if (status == ThreadStatus.Sleeping || status == ThreadStatus.Wait || status == ThreadStatus.Monitor)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_STOPPED;
                else if (status == ThreadStatus.NotStarted)
                    vsthreadState = enum_THREADSTATE.THREADSTATE_FRESH;
                else
                    vsthreadState = enum_THREADSTATE.THREADSTATE_RUNNING;

                ptp[0].dwThreadState = (uint)vsthreadState;
            }

            // suspend count
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_SUSPENDCOUNT) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_SUSPENDCOUNT;
                ptp[0].dwSuspendCount = (uint)suspendCount;
            }

            // affinity
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_AFFINITY) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_AFFINITY;
                ptp[0].AffinityMask = ~0UL;
            }

            // category
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_CATEGORY) != 0)
            {
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_CATEGORY;
                ptp[0].dwThreadCategory = (uint)_category;
            }


            return VSConstants.S_OK;

            throw new NotImplementedException();
        }

        public int SetThreadDisplayName(string bstrDisplayName)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDebugQueryEngine2 Members

        public int GetEngineInterface(out object ppUnk)
        {
            return _program.GetEngineInterface(out ppUnk);
        }

        #endregion
    }
}