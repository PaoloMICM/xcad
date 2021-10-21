﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.Toolkit.PageBuilder.Binders;
using Xarial.XCad.UI;
using Xarial.XCad.UI.PropertyPage.Base;
using Xarial.XCad.Utils.PageBuilder.Core;
using Xarial.XCad.Utils.Reflection;

namespace Xarial.XCad.Utils.PageBuilder.Binders
{
    public class PropertyInfoBinding<TDataModel> : Binding<TDataModel>
    {
        private readonly IList<IControlDescriptor> m_Parents;

        public IControlDescriptor ControlDescriptor { get; }

        private readonly PropertyInfoMetadata m_Metadata;

        private object m_CurrentDataModel;

        internal PropertyInfoBinding(IControl control,
            IControlDescriptor ctrlDesc, IList<IControlDescriptor> parents, PropertyInfoMetadata metadata)
            : base(control)
        {
            ControlDescriptor = ctrlDesc;
            m_Parents = parents;
            m_Metadata = metadata;
        }

        protected override TDataModel DataModel 
        {
            get => base.DataModel; 
            set
            {
                if (m_CurrentDataModel is INotifyPropertyChanged) 
                {
                    (m_CurrentDataModel as INotifyPropertyChanged).PropertyChanged -= OnPropertyChanged;
                }

                base.DataModel = value;

                m_CurrentDataModel = GetCurrentModel();

                if (m_CurrentDataModel is INotifyPropertyChanged)
                {
                    (m_CurrentDataModel as INotifyPropertyChanged).PropertyChanged += OnPropertyChanged;
                }
            }
        }

        public override IMetadata Metadata => m_Metadata;

        protected override void SetDataModelValue(object value)
        {
            var curModel = GetCurrentModel();

            var curVal = ControlDescriptor.GetValue(curModel);
            var destVal = value.Cast(ControlDescriptor.DataType);

            if (!object.Equals(curVal, destVal))
            {
                ControlDescriptor.SetValue(curModel, destVal);
            }
        }

        protected override void SetUserControlValue()
        {
            var curModel = GetCurrentModel();
            var val = ControlDescriptor.GetValue(curModel);

            var curVal = Control.GetValue();

            if (!object.Equals(val, curVal))
            {
                Control.SetValue(val);
            }
        }

        private object GetCurrentModel()
        {
            object curModel = DataModel;

            if (m_Parents != null)
            {
                foreach (var parent in m_Parents)
                {
                    if (curModel == null) 
                    {
                        return null;
                    }

                    var nextModel = parent.GetValue(curModel);

                    if (nextModel == null)
                    {
                        nextModel = Activator.CreateInstance(parent.DataType);
                        parent.SetValue(curModel, nextModel);
                    }

                    curModel = nextModel;
                }
            }

            return curModel;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == ControlDescriptor.Name)
            {
                SetUserControlValue();
                RaiseChangedEvent();
            }
        }
    }
}