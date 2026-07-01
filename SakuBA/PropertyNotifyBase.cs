using System.ComponentModel;
using System.Diagnostics;

namespace SakuBA;

public abstract class PropertyNotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [Conditional("DEBUG")]
    [DebuggerStepThrough]
    private void VerifyPropertyName(string propertyName)
    {
        if (null == TypeDescriptor.GetProperties(this)[propertyName])
        {
            Debug.Fail("Invalid property name: " + propertyName);
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        VerifyPropertyName(propertyName);
        var handler = PropertyChanged;
        if (null != handler)
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}