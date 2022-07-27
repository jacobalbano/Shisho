using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Utility;

public interface IFastAccessor<T>
{
    T GetValue(object target);
    void SetValue(object target, T value);
}

public sealed class FastAccessor : IFastAccessor<object>
{
    public object GetValue(object target) => getMethod(target);
    public void SetValue(object target, object value) => setMethod(target, value);

    public FastAccessor(MemberInfo memberInfo)
    {
        Name = memberInfo.Name;

        if (memberInfo is PropertyInfo propInfo)
        {
            {
                var param = Expression.Parameter(typeof(object));
                var instance = Expression.Convert(param, propInfo.DeclaringType!);
                var convert = Expression.TypeAs(Expression.Property(instance, propInfo), typeof(object));
                getMethod = Expression.Lambda<Func<object, object>>(convert, param).Compile();
            }

            if (propInfo.CanWrite)
            {
                var param = Expression.Parameter(typeof(object));
                var argument = Expression.Parameter(typeof(object));
                var setterCall = Expression.Call(
                    Expression.Convert(param, propInfo.DeclaringType!),
                    propInfo.GetSetMethod()!,
                    Expression.Convert(argument, propInfo.PropertyType));

                setMethod = Expression.Lambda<Action<object, object>>(setterCall, param, argument).Compile();
            }
            else
            {
                setMethod = (self, value) => throw new Exception($"Property {propInfo.Name} cannot be assigned to; it is read-only");
            }
        }
        else if (memberInfo is FieldInfo fieldInfo)
        {
            {
                {
                    var self = Expression.Parameter(typeof(object));
                    var instance = Expression.Convert(self, fieldInfo.DeclaringType!);
                    var field = Expression.Field(instance, fieldInfo);
                    var convert = Expression.TypeAs(field, typeof(object));
                    getMethod = Expression.Lambda<Func<object, object>>(convert, self).Compile();
                }

                {
                    var self = Expression.Parameter(typeof(object));
                    var value = Expression.Parameter(typeof(object));

                    var fieldExp = Expression.Field(Expression.Convert(self, fieldInfo.DeclaringType!), fieldInfo);
                    var assignExp = Expression.Assign(fieldExp, Expression.Convert(value, fieldInfo.FieldType));

                    setMethod = Expression.Lambda<Action<object, object>>(assignExp, self, value).Compile();
                }
            }
        }
        else throw new InvalidOperationException();
    }

    private string Name { get; }
    public override string ToString() => Name;

    private readonly Func<object, object> getMethod;
    private readonly Action<object, object> setMethod;
}

public sealed class FastAccessor<T> : IFastAccessor<T>
{
    public T GetValue(object target) => getMethod(target);
    public void SetValue(object target, T value) => setMethod(target, value);

    public FastAccessor(MemberInfo memberInfo)
    {
        Name = memberInfo.Name;

        if (memberInfo is PropertyInfo propInfo)
        {
            {
                var param = Expression.Parameter(typeof(object));
                var instance = Expression.Convert(param, propInfo.DeclaringType!);
                var prop = Expression.Property(instance, propInfo);
                var toObj = Expression.Convert(prop, typeof(object));
                getMethod = Expression.Lambda<Func<object, T>>(toObj, param).Compile();
            }

            if (propInfo.CanWrite)
            {
                var param = Expression.Parameter(typeof(object));
                var argument = Expression.Parameter(typeof(T));
                var setterCall = Expression.Call(
                    Expression.Convert(param, propInfo.DeclaringType!),
                    propInfo.GetSetMethod()!,
                    Expression.Convert(argument, propInfo.PropertyType));

                setMethod = Expression.Lambda<Action<object, T>>(setterCall, param, argument).Compile();
            }
            else
            {
                setMethod = (self, value) => throw new Exception($"Property {propInfo.Name} cannot be assigned to; it is read-only");
            }
        }
        else if (memberInfo is FieldInfo fieldInfo)
        {
            {
                {
                    var self = Expression.Parameter(typeof(object));
                    var instance = Expression.Convert(self, fieldInfo.DeclaringType!);
                    var field = Expression.Field(instance, fieldInfo);
                    getMethod = Expression.Lambda<Func<object, T>>(field, self).Compile();
                }

                {
                    var self = Expression.Parameter(typeof(object));
                    var value = Expression.Parameter(typeof(T));
                    var fieldExp = Expression.Field(Expression.Convert(self, fieldInfo.DeclaringType!), fieldInfo);
                    var assignExp = Expression.Assign(fieldExp, value);
                    setMethod = Expression.Lambda<Action<object, T>>(assignExp, self, value).Compile();
                }
            }
        }
        else throw new InvalidOperationException();
    }

    private string Name { get; }
    public override string ToString() => Name;

    private readonly Func<object, T> getMethod;
    private readonly Action<object, T> setMethod;
}
