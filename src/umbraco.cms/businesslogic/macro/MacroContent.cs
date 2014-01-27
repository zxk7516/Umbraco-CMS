using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.UI;

namespace umbraco.cms.businesslogic.macro
{
    // represents the content of a macro
    public class MacroContent
    {
        // gets or sets the text content
        public string Text { get; set; }

        // gets or sets the control content
        public Control Control { get; set; }
        public string ControlId { get; set; }

        // gets or sets the date the content was generated
        public DateTime Date { get; set; }

        // a value indicating whether the content is empty
        public bool IsEmpty
        {
            get { return Text == null && Control == null; }
        }

        // a value indicating whether the content is pure text (no control)
        public bool IsText
        {
            get { return Control == null; }
        }

        // a value indicating whether the content is a control
        public bool IsControl
        {
            get { return Control != null; }
        }

        // gets the macro content as a string
        // ie executes the control if any
        public string GetAsText()
        {
            if (Control == null) return Text ?? string.Empty;

            using (var textWriter = new StringWriter())
            using (var htmlWriter = new HtmlTextWriter(textWriter))
            {
                Control.RenderControl(htmlWriter);
                return textWriter.ToString();
            }
        }

        // gets the macro content as a control
        // ie wraps the text in a control if needed
        public Control GetAsControl()
        {
            return Control ?? new LiteralControl(Text ?? string.Empty);
        }
    }
}
