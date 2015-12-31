using DotVVM.Framework.Binding;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Exceptions;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Represents a base class for all DotVVM controls.
    /// </summary>
    public abstract class DotvvmControl : DotvvmBindableObject, IDotvvmControl
    {
        
        /// <summary>
        /// Gets the child controls.
        /// </summary>
        [MarkupOptions(MappingMode = MappingMode.Exclude)]
        public DotvvmControlCollection Children { get; private set; }

        /// <summary>
        /// Gets or sets the unique control ID.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string ID
        {
            get { return (string)GetValue(IDProperty); }
            set { SetValue(IDProperty, value); }
        }

        public static readonly DotvvmProperty IDProperty =
            DotvvmProperty.Register<string, DotvvmControl>(c => c.ID, isValueInherited: false);

        /// <summary>
        /// Gets or sets the client ID generation algorithm.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public ClientIDMode ClientIDMode
        {
            get { return (ClientIDMode)GetValue(ClientIDModeProperty); }
            set { SetValue(ClientIDModeProperty, value); }
        }

        public static readonly DotvvmProperty ClientIDModeProperty =
            DotvvmProperty.Register<ClientIDMode, DotvvmControl>(c => c.ClientIDMode, ClientIDMode.AutoGenerated, isValueInherited: true);
        
        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmControl"/> class.
        /// </summary>
        public DotvvmControl()
        {
            Children = new DotvvmControlCollection(this);
        }

        /// <summary>
        /// Gets this control and all of its descendants.
        /// </summary>
        public IEnumerable<DotvvmControl> GetThisAndAllDescendants(Func<DotvvmControl, bool> enumerateChildrenCondition = null)
        {
            // PERF: non-linear complexity
            yield return this;
            if (enumerateChildrenCondition == null || enumerateChildrenCondition(this))
            {
                foreach (var descendant in GetAllDescendants(enumerateChildrenCondition))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Gets all descendant controls of this control.
        /// </summary>
        public IEnumerable<DotvvmControl> GetAllDescendants(Func<DotvvmControl, bool> enumerateChildrenCondition = null)
        {
            // PERF: non-linear complexity
            foreach (var child in Children)
            {
                yield return child;

                if (enumerateChildrenCondition == null || enumerateChildrenCondition(child))
                {
                    foreach (var grandChild in child.GetAllDescendants())
                    {
                        yield return grandChild;
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the control has only white space content.
        /// </summary>
        public bool HasOnlyWhiteSpaceContent()
        {
            return Children.All(c => (c is RawLiteral && ((RawLiteral)c).IsWhitespace));
        }

        /// <summary>
        /// Renders the control into the specified writer.
        /// </summary>
        public virtual void Render(IHtmlWriter writer, RenderContext context)
        {
            if (Properties.ContainsKey(PostBack.UpdateProperty))
            {
                // the control might be updated on postback, add the control ID
                EnsureControlHasId();
            }

            try
            {
                RenderControl(writer, context);
            }
            catch (DotvvmControlException) { throw; }
            catch (Exception e)
            {
                throw new DotvvmControlException(this, "Error occured in Render method", e);
            }
        }

        /// <summary>
        /// Renders the control into the specified writer.
        /// </summary>
        protected virtual void RenderControl(IHtmlWriter writer, RenderContext context)
        {
            RenderBeginWithDataBindAttribute(writer);

            foreach (var item in properties)
            {
                if (item.Key is ActiveDotvvmProperty)
                {
                    ((ActiveDotvvmProperty)item.Key).AddAttributesToRender(writer, context, GetValue(item.Key), this);
                }
            }

            AddAttributesToRender(writer, context);
            RenderBeginTag(writer, context);
            RenderContents(writer, context);
            RenderEndTag(writer, context);

            RenderEndWithDataBindAttribute(writer);
        }

        private void RenderBeginWithDataBindAttribute(IHtmlWriter writer)
        {
            // if the DataContext is set, render the "with" binding
            if (HasBinding(DataContextProperty))
            {
                writer.WriteKnockoutWithComment(GetValueBinding(DataContextProperty).GetKnockoutBindingExpression());
            }
        }

        private void RenderEndWithDataBindAttribute(IHtmlWriter writer)
        {
            if (HasBinding(DataContextProperty))
            {
                writer.WriteKnockoutDataBindEndComment();
            }
        }


        /// <summary>
        /// Adds all attributes that should be added to the control begin tag.
        /// </summary>
        protected virtual void AddAttributesToRender(IHtmlWriter writer, RenderContext context)
        {
        }

        /// <summary>
        /// Renders the control begin tag.
        /// </summary>
        protected virtual void RenderBeginTag(IHtmlWriter writer, RenderContext context)
        {
        }

        /// <summary>
        /// Renders the contents inside the control begin and end tags.
        /// </summary>
        protected virtual void RenderContents(IHtmlWriter writer, RenderContext context)
        {
            RenderChildren(writer, context);
        }

        /// <summary>
        /// Renders the control end tag.
        /// </summary>
        protected virtual void RenderEndTag(IHtmlWriter writer, RenderContext context)
        {
        }

        /// <summary>
        /// Renders the children.
        /// </summary>
        protected void RenderChildren(IHtmlWriter writer, RenderContext context)
        {
            foreach (var child in Children)
            {
                child.Render(writer, context);
            }
        }

        /// <summary>
        /// Ensures that the control has ID. The method can auto-generate it, if specified.
        /// </summary>
        public void EnsureControlHasId(bool autoGenerate = true)
        {
            if (autoGenerate && string.IsNullOrEmpty(ID))
            {
                ID = AutoGenerateControlId();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ID))
                {
                    throw new DotvvmControlException(this, $"The control of type '{GetType().FullName}' must have ID!");
                }
                if (!Regex.IsMatch(ID, "^[a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    throw new DotvvmControlException(this, $"The control ID '{ID}' is not valid! It can contain only characters, numbers and the underscore character, and it cannot start with a number!");
                }
            }
        }

        /// <summary>
        /// Generates unique control ID automatically.
        /// </summary>
        private string AutoGenerateControlId()
        {
            return GetValue(Internal.UniqueIDProperty).ToString();
        }

        /// <summary>
        /// Finds the control by its ID.
        /// </summary>
        public DotvvmControl FindControl(string id, bool throwIfNotFound = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var control = GetAllDescendants(c => !IsNamingContainer(c)).SingleOrDefault(c => c.ID == id);
            if (control == null && throwIfNotFound)
            {
                throw new Exception(string.Format("The control with ID '{0}' was not found.", id));
            }
            return control;
        }

        /// <summary>
        /// Finds the control by its ID.
        /// </summary>
        public T FindControl<T>(string id, bool throwIfNotFound = false) where T : DotvvmControl
        {
            var control = FindControl(id, throwIfNotFound);
            if (!(control is T))
            {
                throw new DotvvmControlException(this, $"The control with ID '{id}' was found, however it is not an instance of the desired type '{typeof(T)}'.");
            }
            return (T)control;
        }

        /// <summary>
        /// Finds the control by its unique ID.
        /// </summary>
        public DotvvmControl FindControlByUniqueId(string controlUniqueId)
        {
            var parts = controlUniqueId.Split('_');
            DotvvmControl result = this;
            for (var i = 0; i < parts.Length; i++)
            {
                result = result.FindControl(parts[i]);
                if (result == null)
                {
                    return null;
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the naming container of the current control.
        /// </summary>
        public DotvvmControl GetNamingContainer()
        {
            var control = this;
            while (!IsNamingContainer(control) && control.Parent != null)
            {
                control = control.Parent;
            }
            return control;
        }

        /// <summary>
        /// Determines whether the specified control is a naming container.
        /// </summary>
        public static bool IsNamingContainer(DotvvmControl control)
        {
            return (bool)control.GetValue(Internal.IsNamingContainerProperty);
        }

        /// <summary>
        /// Occurs after the viewmodel tree is complete.
        /// </summary>
        internal virtual void OnPreInit(IDotvvmRequestContext context)
        {
            foreach (var property in GetDeclaredProperties())
            {
                property.OnControlInitialized(this);
            }
        }

        /// <summary>
        /// Called right before the rendering shall occur.
        /// </summary>
        internal virtual void OnPreRenderComplete(IDotvvmRequestContext context)
        {
            // events on properties
            foreach (var property in GetDeclaredProperties())
            {
                property.OnControlRendering(this);
            }
        }

        /// <summary>
        /// Occurs before the viewmodel is applied to the page.
        /// </summary>
        protected internal virtual void OnInit(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Occurs after the viewmodel is applied to the page and before the commands are executed.
        /// </summary>
        protected internal virtual void OnLoad(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Occurs after the page commands are executed.
        /// </summary>
        protected internal virtual void OnPreRender(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Gets the client ID of the control. Returns null if the ID cannot be calculated.
        /// </summary>
        public string GetClientId()
        {
            if (!string.IsNullOrEmpty(ID))
            {
                // build the client ID
                var fragments = GetClientIdFragments();
                if (fragments.Any(f => f.IsExpression))
                {
                    return null;
                }

                return ComposeStaticClientId(fragments);
            }
            return null;
        }

        private static string ComposeStaticClientId(List<ClientIDFragment> fragments)
        {
            var sb = new StringBuilder();
            for (int i = fragments.Count - 1; i >= 0; i--)
            {
                if (sb.Length > 0)
                {
                    sb.Append("_");
                }
                sb.Append(fragments[i].Value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Adds the corresponding attribute for the Id property.
        /// </summary>
        protected virtual void RenderClientId(IHtmlWriter writer)
        {
            if (!string.IsNullOrEmpty(ID))
            {
                // build the client ID
                var fragments = GetClientIdFragments();
                if (!fragments.Any(f => f.IsExpression))
                {
                    // generate ID attribute
                    writer.AddAttribute("id", ComposeStaticClientId(fragments));
                }
                else
                {
                    // generate ID binding
                    var sb = new StringBuilder();
                    for (int i = fragments.Count - 1; i >= 0; i--)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(",");
                        }

                        if (fragments[i].IsExpression)
                        {
                            sb.Append(fragments[i].Value);
                        }
                        else
                        {
                            sb.Append("'");
                            sb.Append(fragments[i].Value);
                            sb.Append("'");
                        }
                    }
                    var group = new KnockoutBindingGroup();
                    group.Add("id", $"dotvvm.evaluator.buildClientId(this, [{sb.ToString()}])");
                    writer.AddKnockoutDataBind("attr", group);
                }
            }
        }

        private List<ClientIDFragment> GetClientIdFragments()
        {
            var dataContextChanges = 0;
            var fragments = new List<ClientIDFragment>();
            foreach (var ancestor in new[] { this }.Concat(GetAllAncestors()))
            {
                if (ancestor.HasBinding(DataContextProperty))
                {
                    dataContextChanges++;
                }

                if (this == ancestor || IsNamingContainer(ancestor))
                {
                    var clientIdExpression = (string) ancestor.GetValue(Internal.ClientIDFragmentProperty);
                    if (clientIdExpression != null)
                    {
                        // generate the expression
                        var expression = new StringBuilder();
                        for (int i = 0; i < dataContextChanges; i++)
                        {
                            expression.Append("$parentContext.");
                        }
                        expression.Append(clientIdExpression);
                        fragments.Add(new ClientIDFragment() { Value = expression.ToString(), IsExpression = true });
                    }
                    else if (!string.IsNullOrEmpty(ancestor.ID))
                    {
                        // add the ID fragment
                        fragments.Add(new ClientIDFragment() { Value = ancestor.ID });
                    }
                }

                if (ancestor.ClientIDMode == ClientIDMode.Static)
                {
                    break;
                }
            }
            return fragments;
        }

        /// <summary>
        /// Verifies that the control contains only a plain text content and tries to extract it.
        /// </summary>
        protected bool TryGetTextContent(out string textContent)
        {
            textContent = string.Join(string.Empty, Children.OfType<RawLiteral>().Where(l => !l.IsWhitespace).Select(l => l.UnencodedText));
            return Children.All(c => c is RawLiteral);
        }

        public override IEnumerable<DotvvmBindableObject> GetLogicalChildren()
        {
            return Children;
        }
    }
}