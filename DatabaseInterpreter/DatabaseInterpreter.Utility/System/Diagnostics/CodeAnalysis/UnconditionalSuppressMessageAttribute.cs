using System;

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            this.Category = category;
            this.CheckId = checkId;
        }

        public string Category { get; }
        public string CheckId { get; }
        public string Justification { get; set; }
        public string MessageId { get; set; }
        public string Scope { get; set; }
        public string Target { get; set; }
    }
}
