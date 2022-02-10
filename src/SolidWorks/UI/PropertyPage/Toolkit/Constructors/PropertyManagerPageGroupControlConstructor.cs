﻿//*********************************************************************
//xCAD
//Copyright(C) 2022 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using Xarial.XCad.SolidWorks.UI.PropertyPage.Toolkit.Controls;
using Xarial.XCad.UI.PropertyPage.Attributes;
using Xarial.XCad.UI.PropertyPage.Base;
using Xarial.XCad.Utils.PageBuilder.Attributes;
using Xarial.XCad.Utils.PageBuilder.Base;
using Xarial.XCad.Utils.PageBuilder.Constructors;
using Xarial.XCad.Utils.PageBuilder.Core;

namespace Xarial.XCad.SolidWorks.UI.PropertyPage.Toolkit.Constructors
{
    [DefaultType(typeof(SpecialTypes.ComplexType))]
    internal class PropertyManagerPageGroupControlConstructor
        : GroupConstructor<PropertyManagerPageGroupBase, PropertyManagerPagePage>,
        IPropertyManagerPageElementConstructor
    {
        public Type ControlType
        {
            get
            {
                return typeof(PropertyManagerPageGroupControl);
            }
        }

        public void PostProcessControls(IEnumerable<IPropertyManagerPageControlEx> ctrls)
        {
            //TODO: not used
        }

        protected override PropertyManagerPageGroupBase Create(
            PropertyManagerPageGroupBase group, IAttributeSet atts, IMetadata[] metadata, ref int numberOfUsedIds)
        {
            if (group is PropertyManagerPageTabControl)
            {
                var opts = GetGroupOptions(atts, metadata, out IMetadata toggleMetadata);

                var grp = (group as PropertyManagerPageTabControl).Tab.AddGroupBox(atts.Id, atts.Name,
                    (int)opts) as IPropertyManagerPageGroup;

                return new PropertyManagerPageGroupControl(atts.Id, atts.Tag,
                    group.Handler, grp, group.App, group.ParentPage, toggleMetadata,
                    opts.HasFlag(swAddGroupBoxOptions_e.swGroupBoxOptions_Checkbox), 
                    !opts.HasFlag(swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded), metadata);
            }
            //NOTE: nested groups are not supported in SOLIDWORKS, creating the group in page instead
            else if (group is PropertyManagerPageGroupControl)
            {
                return Create(group.ParentPage, atts, metadata, ref numberOfUsedIds);
            }
            else
            {
                throw new NullReferenceException("Not supported group type");
            }
        }

        protected override PropertyManagerPageGroupBase Create(PropertyManagerPagePage page, IAttributeSet atts,
            IMetadata[] metadata, ref int numberOfUsedIds)
        {
            var opts = GetGroupOptions(atts, metadata, out IMetadata toggleMetadata);

            var grp = page.Page.AddGroupBox(atts.Id, atts.Name,
                (int)opts) as IPropertyManagerPageGroup;

            return new PropertyManagerPageGroupControl(atts.Id, atts.Tag,
                page.Handler, grp, page.App, page, toggleMetadata,
                opts.HasFlag(swAddGroupBoxOptions_e.swGroupBoxOptions_Checkbox),
                !opts.HasFlag(swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded), metadata);
        }

        private swAddGroupBoxOptions_e GetGroupOptions(IAttributeSet atts, IMetadata[] metadata, out IMetadata toggleMetadata) 
        {
            GroupBoxOptions_e opts = 0;

            if (atts.Has<IGroupBoxOptionsAttribute>()) 
            {
                opts = atts.Get<IGroupBoxOptionsAttribute>().Options;
            }

            var swOpts = swAddGroupBoxOptions_e.swGroupBoxOptions_Visible;

            if (!opts.HasFlag(GroupBoxOptions_e.Collapsed)) 
            {
                swOpts |= swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded;
            }

            if (atts.Has<ICheckableGroupBoxAttribute>())
            {
                var checkAtt = atts.Get<ICheckableGroupBoxAttribute>();

                swOpts |= swAddGroupBoxOptions_e.swGroupBoxOptions_Checkbox;
                toggleMetadata = metadata?.FirstOrDefault(m => object.Equals(m.Tag, checkAtt.ToggleMetadataTag));

                if (toggleMetadata == null) 
                {
                    throw new NullReferenceException($"Failed to find the metadata to drive group toggle: '{checkAtt.ToggleMetadataTag}'");
                }
            }
            else 
            {
                toggleMetadata = null;
            }

            return swOpts;
        }
    }
}