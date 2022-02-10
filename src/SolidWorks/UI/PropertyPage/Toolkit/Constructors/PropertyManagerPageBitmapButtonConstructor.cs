﻿//*********************************************************************
//xCAD
//Copyright(C) 2022 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Drawing;
using System.Linq;
using Xarial.XCad.SolidWorks.Services;
using Xarial.XCad.SolidWorks.UI.PropertyPage.Toolkit.Controls;
using Xarial.XCad.SolidWorks.UI.PropertyPage.Toolkit.Icons;
using Xarial.XCad.SolidWorks.Utils;
using Xarial.XCad.UI.PropertyPage.Attributes;
using Xarial.XCad.UI.PropertyPage.Base;
using Xarial.XCad.Utils.PageBuilder.Attributes;
using Xarial.XCad.Utils.PageBuilder.Base;

namespace Xarial.XCad.SolidWorks.UI.PropertyPage.Toolkit.Constructors
{
    internal class PropertyManagerPageBitmapButtonConstructor
        : PropertyManagerPageBaseControlConstructor<PropertyManagerPageBitmapButtonControl, IPropertyManagerPageBitmapButton>, IBitmapButtonConstructor
    {
        private readonly IIconsCreator m_IconsConv;

        public PropertyManagerPageBitmapButtonConstructor(ISldWorks app, IIconsCreator iconsConv)
            : base(app, swPropertyManagerPageControlType_e.swControlType_BitmapButton, iconsConv)
        {
            m_IconsConv = iconsConv;
        }

        protected override IPropertyManagerPageBitmapButton CreateSwControl(object host, ControlOptionsAttribute opts, IAttributeSet atts)
        {
            SetButtonSpecificType(atts);
            return base.CreateSwControl(host, opts, atts);
        }
        
        private void SetButtonSpecificType(IAttributeSet atts) 
        {
            if (atts.ContextType == typeof(bool))
            {
                m_Type = swPropertyManagerPageControlType_e.swControlType_CheckableBitmapButton;
            }
            else if (atts.ContextType == typeof(Action))
            {
                m_Type = swPropertyManagerPageControlType_e.swControlType_BitmapButton;
            }
            else 
            {
                throw new NotSupportedException("Only bool and Action types are supported for bitmap button");
            }
        }

        protected override PropertyManagerPageBitmapButtonControl CreateControl(
            IPropertyManagerPageBitmapButton swCtrl, IAttributeSet atts, IMetadata[] metadata,
            SwPropertyManagerPageHandler handler, short height, IPropertyManagerPageLabel label)
        {
            var bmpAtt = atts.Get<BitmapButtonAttribute>();

            if (bmpAtt.StandardIcon.HasValue)
            {
                swCtrl.SetStandardBitmaps((int)bmpAtt.StandardIcon.Value);
            }
            else
            {
                var bmpWidth = bmpAtt.Width;
                var bmpHeight = bmpAtt.Height;

                var icon = bmpAtt.Icon ?? Defaults.Icon;

                if (m_App.IsVersionNewerOrEqual(Enums.SwVersion_e.Sw2016))
                {
                    var icons = m_IconsConv.ConvertIcon(new BitmapButtonHighResIcon(icon, bmpWidth, bmpHeight));

                    var imgList = icons.Take(6).ToArray();
                    var maskImgList = icons.Skip(6).ToArray();
                    swCtrl.SetBitmapsByName3(imgList, maskImgList);
                }
                else
                {
                    var icons = m_IconsConv.ConvertIcon(new BitmapButtonIcon(icon, bmpWidth, bmpHeight));

                    swCtrl.SetBitmapsByName2(icons[0], icons[1]);
                }
            }

            return new PropertyManagerPageBitmapButtonControl(atts.Id, atts.Tag, swCtrl, handler, label, metadata);
        }
    }
}