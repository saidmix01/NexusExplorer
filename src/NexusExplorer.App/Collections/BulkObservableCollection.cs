using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NexusExplorer.App.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can add many items at once while raising a
/// single <see cref="NotifyCollectionChangedAction.Reset"/> notification instead of one
/// notification per item.
///
/// Populating a plain ObservableCollection with <c>foreach item Add(item)</c> raises N
/// CollectionChanged events, each of which forces the bound ItemsControl to realize a container
/// synchronously. For large directories (thousands of files) that is a dominant cost on the UI
/// thread. <see cref="AddRange"/> collapses that into a single layout/measure pass.
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    /// <summary>
    /// Adds all <paramref name="items"/> in one batch, raising a single Reset notification at
    /// the end (or none if the sequence was empty).
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items is null) return;

        _suppressNotification = true;
        var added = false;
        try
        {
            foreach (var item in items)
            {
                Items.Add(item);
                added = true;
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        if (added)
            RaiseReset();
    }

    /// <summary>
    /// Clears the collection and repopulates it from <paramref name="items"/> with a single
    /// Reset notification, avoiding a Clear-then-N-Adds notification storm.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        _suppressNotification = true;
        try
        {
            Items.Clear();
            if (items is not null)
            {
                foreach (var item in items)
                    Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnPropertyChanged(e);
    }
}
