﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Xarial.XCad.Enums;
using Xarial.XCad.SolidWorks.Enums;
using Xarial.XCad.SolidWorks.Exceptions;
using Xarial.XCad.Toolkit.Windows;

namespace Xarial.XCad.SolidWorks.Utils
{
    internal class SwApplicationStarter : IDisposable
    {
        private readonly ApplicationState_e m_State;
        private readonly ISwVersion m_Version;

        internal SwApplicationStarter(ApplicationState_e state, ISwVersion version) 
        {
            m_State = state;
            m_Version = version;
        }

        internal ISldWorks Start(Action<Process> startHandler, CancellationToken cancellationToken) 
        {
            SwVersion_e? vers = null;

            if (m_Version != null)
            {
                vers = m_Version.Major;
            }

            var args = new List<string>();

            if (m_State.HasFlag(ApplicationState_e.Safe))
            {
                args.Add(SwApplicationFactory.CommandLineArguments.SafeMode);
            }

            if (m_State.HasFlag(ApplicationState_e.Silent))
            {
                args.Add(SwApplicationFactory.CommandLineArguments.SilentMode);
            }

            if (m_State.HasFlag(ApplicationState_e.Background))
            {
                args.Add(SwApplicationFactory.CommandLineArguments.BackgroundMode);
            }

            var swPath = FindSwAppPath(vers);

            var prcInfo = new ProcessStartInfo(swPath, string.Join(" ", args));

            if (m_State.HasFlag(ApplicationState_e.Hidden))
            {
                prcInfo.UseShellExecute = false;
                prcInfo.CreateNoWindow = true;
                prcInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }

            var prc = Process.Start(prcInfo);

            startHandler.Invoke(prc);

            try
            {
                ISldWorks app;
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new AppStartCancelledByUserException();
                    }

                    app = RotHelper.TryGetComObjectByMonikerName<ISldWorks>(SwApplicationFactory.GetMonikerName(prc));
                    Thread.Sleep(100);
                }
                while (app == null);

                if (m_State.HasFlag(ApplicationState_e.Hidden)) 
                {
                    app.Visible = false;
                }

                return app;
            }
            catch
            {
                if (prc != null)
                {
                    try
                    {
                        prc.Kill();
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        private string FindSwAppPath(SwVersion_e? vers)
        {
            RegistryKey swAppRegKey = null;

            if (vers.HasValue)
            {
                var progId = string.Format(SwApplicationFactory.PROG_ID_TEMPLATE, (int)vers);
                swAppRegKey = Registry.ClassesRoot.OpenSubKey(progId);
            }
            else
            {
                foreach (var versCand in Enum.GetValues(typeof(SwVersion_e)).Cast<int>().OrderByDescending(x => x))
                {
                    var progId = string.Format(SwApplicationFactory.PROG_ID_TEMPLATE, versCand);
                    swAppRegKey = Registry.ClassesRoot.OpenSubKey(progId);

                    if (swAppRegKey != null)
                    {
                        break;
                    }
                }
            }

            if (swAppRegKey != null)
            {
                return SwApplicationFactory.FindSwPathFromRegKey(swAppRegKey);
            }
            else
            {
                throw new NullReferenceException("Failed to find the information about the installed SOLIDWORKS applications in the registry");
            }
        }

        public void Dispose()
        {
            
        }
    }
}
