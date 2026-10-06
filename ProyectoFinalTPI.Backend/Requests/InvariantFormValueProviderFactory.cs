using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProyectoFinalTPI.Backend.Requests;

public sealed class InvariantFormValueProviderFactory : IValueProviderFactory
{
    public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        var request = context.ActionContext.HttpContext.Request;
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            context.ValueProviders.Add(new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture));
        }
    }
}
