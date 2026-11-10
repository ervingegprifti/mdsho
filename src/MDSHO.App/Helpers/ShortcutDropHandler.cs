using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>
  /// The drop target of a window's shortcut tree (gong-wpf-dragdrop): decides whether and how items may be dropped and
  /// performs the drop. Moving tree items and making shortcuts from dropped files and links is done by the view model.
  /// </summary>
  public sealed class ShortcutDropHandler : IDropTarget {

    /// <summary>The window (or clone) whose tree this handler serves.</summary>
    public DesktopWindowViewModel Window { get; }
    private readonly DesktopWindowViewModel window;

    /// <summary>Creates the handler for a window's view model.</summary>
    public ShortcutDropHandler(DesktopWindowViewModel window) {
      this.window = window;
      Window = window;
    }

    /// <summary>Tells the drag and drop library whether, and how, the dragged items may be dropped at the current position.</summary>
    public void DragOver(IDropInfo dropInfo) {
      // To allow a drop at the current drag position,
      // DropInfo.Effects property on dropInfo should be set to a value other than DragDropEffects.None, DropInfo.Data should be set to a non-null value.

      // Your target has to set a DragDropEffect in the DragOver handler to allow the drop:
      // To alow drop from control panel items set Effects to DragDropEffects.Link

      if (CanAcceptData(dropInfo)) {
        dropInfo.Effects = GetDragDropEffects(dropInfo);
        var isTreeViewItem = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) && dropInfo.VisualTargetItem is TreeViewItem;
        if (isTreeViewItem) {
          // Highlight -> Is the rectangular border surrounding the item.
          dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
        } else {
          // Insert -> Is the line indicator
          dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        }
      }
    }
    /// <summary>True when the dragged data (shortcuts from the tree, or files and links from outside) may be dropped on the target.</summary>
    public static bool CanAcceptData(IDropInfo dropInfo) {
      // Check: There is nothing we can do here.
      if (dropInfo == null) {
        return false;
      }

      // Check: Data is populated from external and internal drag & drop. If it is null there is nothing we can do here.
      if (dropInfo.Data == null) {
        return false;
      }

      // Check: If drop is on top of a TreeViewItem and if that TrieViewItem is not a group then we should not allow drop.
      bool targetIsTreeViewItem = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) && dropInfo.VisualTargetItem is TreeViewItem;
      if (targetIsTreeViewItem) {
        TreeViewItem treeViewItem = (TreeViewItem)dropInfo.VisualTargetItem;
        if (treeViewItem.DataContext is ShortcutViewModel) {
          ShortcutViewModel treeViewItemShortcut = (ShortcutViewModel)treeViewItem.DataContext;
          if (!treeViewItemShortcut.IsGroup) {
            return false;
          }
        }
      }

      // Target is group proced with the other checks...

      // ------------------------------------------------------------------
      // Check if Drag & Drop is from external source
      // ------------------------------------------------------------------
      if (dropInfo.Data is DataObject) {
        return true;
      }

      // ------------------------------------------------------------------
      // Drag & Drop is from internal source
      // ------------------------------------------------------------------
      // Prevent target is in the same context as the source
      if (!dropInfo.IsSameDragDropContextAsSource) {
        return false;
      }

      // do not drop on itself
      if (targetIsTreeViewItem && dropInfo.VisualTargetItem == dropInfo.DragInfo.VisualSourceItem) {
        return false;
      }

      //
      if (dropInfo.DragInfo.SourceCollection == dropInfo.TargetCollection) {
        var targetList = dropInfo.TargetCollection.TryGetList();
        return targetList != null;
      }
      else if (dropInfo.TargetCollection == null) {
        return false;
      } else {
        if (TestCompatibleTypes(dropInfo.TargetCollection, dropInfo.Data)) {
          var isChildOf = IsChildOf(dropInfo.VisualTargetItem, dropInfo.DragInfo.VisualSourceItem);
          return !isChildOf;
        } else {
          return false;
        }
      }
    }
    /// <summary>Link for external data, otherwise Copy or Move depending on the keys held.</summary>
    public static DragDropEffects GetDragDropEffects(IDropInfo dropInfo) {
      // dropInfo should be never null here
      // dropInfo.Data should be never null here

      // ------------------------------------------------------------------
      // Check if the  Drag & Drop is from external source
      // ------------------------------------------------------------------
      if (dropInfo.Data is DataObject) {
        // We use link if the  Drag & Drop is from external source
        return DragDropEffects.Link;
      }

      // ------------------------------------------------------------------
      // Drag & Drop is from internal source
      // ------------------------------------------------------------------
      // If it is Drag & Drop from the internal source then use the default library behaviour
      var copyData = ((dropInfo.DragInfo.DragDropCopyKeyState != default(DragDropKeyStates)) && dropInfo.KeyStates.HasFlag(dropInfo.DragInfo.DragDropCopyKeyState))
                     ||
                     dropInfo.DragInfo.DragDropCopyKeyState.HasFlag(DragDropKeyStates.LeftMouseButton);
      copyData = copyData
                 //&& (dropInfo.DragInfo.VisualSource != dropInfo.VisualTarget)
                 && !(dropInfo.DragInfo.SourceItem is HeaderedContentControl)
                 && !(dropInfo.DragInfo.SourceItem is HeaderedItemsControl)
                 && !(dropInfo.DragInfo.SourceItem is ListBoxItem);

      if (copyData) {
        return DragDropEffects.Copy;
      } else {
        return DragDropEffects.Move;
      }
    }
    /// <summary>The dragged data as a sequence (a single object becomes a sequence of one).</summary>
    public static IEnumerable ExtractData(object data) {
      if (data is IEnumerable && !(data is string)) {
        return (IEnumerable)data;
      } else {
        return Enumerable.Repeat(data, 1);
      }
    }
    /// <summary>True when the target item sits inside the source item in the tree.</summary>
    private static bool IsChildOf(UIElement targetItem, UIElement sourceItem) {
      var parent = ItemsControl.ItemsControlFromItemContainer(targetItem);
      while (parent != null) {
        if (parent == sourceItem) {
          return true;
        }
        parent = ItemsControl.ItemsControlFromItemContainer(parent);
      }
      return false;
    }
    /// <summary>True when the dragged data fits the items of the target collection.</summary>
    private static bool TestCompatibleTypes(IEnumerable target, object data) {
      TypeFilter filter = (t, o) => {
        return (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
      };

      var enumerableInterfaces = target.GetType().FindInterfaces(filter, null);
      var enumerableTypes = from i in enumerableInterfaces select i.GetGenericArguments().Single();

      if (enumerableTypes.Count() > 0) {
        var dataType = TypeUtilities.GetCommonBaseClass(ExtractData(data));
        return enumerableTypes.Any(t => t.IsAssignableFrom(dataType));
      } else {
        return target is IList;
      }
    }
    /// <summary>Performs a drop: moves tree items or creates shortcuts from dropped files, folders and web links, all in one transaction.</summary>
    public void Drop(IDropInfo dropInfo) {
      // Check: There is nothing we can do here.
      if (dropInfo == null) {
        return;
      }

      // Check: Data is populated from external and internal drag & drop. If it is null there is nothing we can do here.
      if (dropInfo.Data == null) {
        return;
      }

      // Check: If drop is on top of a TreeViewItem and if that TrieViewItem is not a group then we should not allow drop.
      if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) && dropInfo.VisualTargetItem is TreeViewItem) {
        TreeViewItem treeViewItem = (TreeViewItem)dropInfo.VisualTargetItem;
        if (treeViewItem.DataContext is ShortcutViewModel) {
          ShortcutViewModel treeViewItemShortcut = (ShortcutViewModel)treeViewItem.DataContext;
          if (!treeViewItemShortcut.IsGroup) {
            return;
          }
        }
      }

      // destinationList = 0 -> Dropping into an empty RootShortcuts list.
      // destinationList = 0 -> Dropping into an empty Children list.
      // destinationList > 0 -> Dropping into RootShortcuts list.
      // destinationList > 0 -> Dropping into Children list.
      IList destinationList = dropInfo.TargetCollection.TryGetList();

      // Root Shortcut:
      // shortcutParentShortcut NULL
      // shortcutParentWindow NOT NULL
      //
      // Non Root Shortcut:
      // shortcutParentShortcut NOT NULL
      // shortcutParentWindow NULL
      ShortcutViewModel shortcutParentShortcut = null;
      DesktopWindowViewModel shortcutParentWindow = window.IsClone ? window.ParentWindow : window;
      if (dropInfo.VisualTargetItem == null) {
        // dropInfo.VisualTargetItem is null -> The TreeViewShortcuts has no items and this is the first time an item is draged.
        // dropInfo.VisualTargetItem is null -> The TreeViewShortcuts has items and the item is draged at the bottom of the root items list.
        // Root Shortcut:
        shortcutParentShortcut = null;
        shortcutParentWindow = window.IsClone ? window.ParentWindow : window;
      } else {
        // dropInfo.VisualTargetItem is NOT null -> The TreeViewShortcuts has items and the item is draged NOT at the bottom of the root items list.
        if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) && dropInfo.VisualTargetItem is TreeViewItem) {
          // Drop is on top of a TreeViewItem.
          if (dropInfo.VisualTargetItem is TreeViewItem) {
            TreeViewItem treeViewItem = (TreeViewItem)dropInfo.VisualTargetItem;
            if (treeViewItem.DataContext is ShortcutViewModel) {
              // Non Root Shortcut:
              shortcutParentShortcut = (ShortcutViewModel)treeViewItem.DataContext;
              shortcutParentWindow = null;
            }
          }
        } else {
          // Drop is NOT on top of a TreeViewItem but in between TreeViewItems
          if (destinationList.Count > 0) {
            // We just get the first element and check its ParentShortcut and ParentWindow.
            if (destinationList[0] is ShortcutViewModel) {
              ShortcutViewModel destinationListFirstShortcut = (ShortcutViewModel)destinationList[0];
              shortcutParentShortcut = destinationListFirstShortcut.ParentShortcut;
              shortcutParentWindow = destinationListFirstShortcut.ParentWindow;
            }
          } else {
            // destinationList.Count = 0 is not possible here.
          }
        }
      }

      // ++insertIndex means that when your code is executing it will first do insertIndex = insertIndex + 1 and then read it.
      var insertIndex = dropInfo.InsertIndex != dropInfo.UnfilteredInsertIndex ? dropInfo.UnfilteredInsertIndex : dropInfo.InsertIndex;
      var itemsControl = dropInfo.VisualTarget as ItemsControl;
      if (itemsControl != null) {
        var editableItems = itemsControl.Items as IEditableCollectionView;
        if (editableItems != null) {
          var newItemPlaceholderPosition = editableItems.NewItemPlaceholderPosition;
          if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning && insertIndex == 0) {
            ++insertIndex;
          } else if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtEnd && insertIndex == itemsControl.Items.Count) {
            --insertIndex;
          }
        }
      }

      List<object> data = ExtractData(dropInfo.Data).OfType<object>().ToList();

      if (destinationList == null) {
        return;
      }

      // Nothing below touches the collections or the database until ViewModelFactory.ApplyShortcutMove:
      // it writes everything in ONE transaction and only then changes the collections (all or nothing).
      bool external = dropInfo.Data is DataObject;
      IList sourceList = null;
      List<ShortcutViewModel> moved = new List<ShortcutViewModel>();
      Action<List<ShortcutViewModel>> create = null;
      if (!external) {
        // Internal Drag & Drop (item from the treeview itself).
        List<ShortcutViewModel> dragged = data.OfType<ShortcutViewModel>().ToList();
        if (GetDragDropEffects(dropInfo) == DragDropEffects.Copy) {
          // Ctrl + drag: copies (with their files) go to the drop place; the originals stay where they are.
          create = (created) => {
            List<string> copiedFiles = new List<string>();
            try {
              foreach (ShortcutViewModel shortcut in dragged) {
                created.Add(ViewModelFactory.DeepCloneShortcut(shortcut, shortcutParentShortcut, shortcutParentWindow, copiedFiles.Add));
              }
            } catch (Exception) {
              foreach (string file in copiedFiles) {
                try { System.IO.File.Delete(file); } catch (Exception) { }
              }
              throw;
            }
          };
        } else {
          // A move: the source items leave the source list.
          if (dropInfo.DragInfo != null) {
            sourceList = dropInfo.DragInfo.SourceCollection.TryGetList();
          }
          moved = dragged;
        }
      }

      if (external) {
        create = (created) => {
          foreach (var o in data) {
            // ----------------------------------------------
            // This is a Drag & Drop from the external source.
            // ----------------------------------------------
            DataObject dataObject = o as DataObject;
            if (dataObject != null) {
              if (dataObject.ContainsFileDropList() && dataObject.GetDataPresent(DataFormats.FileDrop, true)) {
                // ******************************************************************************************************************************************
                // FileDrop. Queries a data object for the presence of data in the System.Windows.DataFormats.FileDrop data format.
                // ******************************************************************************************************************************************
                // We can have more than one file dropped.
                var droppedFilePaths = dataObject.GetFileDropList();
                window.AddDroppedFiles(droppedFilePaths.Cast<string>(), shortcutParentShortcut, shortcutParentWindow, created);
              } else if (dataObject.ContainsText() && dataObject.GetDataPresent(DataFormats.Text, true)) {
                // ******************************************************************************************************************************************
                // AbsoluteUri. Queries a data object for the presence of data in the System.Windows.DataFormats.UnicodeText format.
                // ******************************************************************************************************************************************
                string dataText = (string)dataObject.GetData(DataFormats.Text, true);
                window.AddDroppedText(dataText, shortcutParentShortcut, shortcutParentWindow, created);
              } else {
                // ******************************************************************************************************************************************
                // ShellIDListArray. Perhaps data contain virtual files
                // ******************************************************************************************************************************************
                // Returns a list of formats in which the data in this data object is stored, or can be converted to.
                string[] formats = dataObject.GetFormats();
                foreach (string format in formats) {
                  if (format == "Shell IDList Array") {
                    // Virtual file/s been draged.
                    string message = "To create this kind of shortcut please do:\n";
                    message += "1. Drag to create a desktop shortcut first,\n";
                    message += "2. Drag the shortcut from desktop to MDSHO";
                    MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                  }
                }
              }
            }
          }
        };
      }

      // Apply: database (one transaction) first, then the treeview and view model changes.
      ViewModelFactory.ApplyShortcutMove(sourceList, destinationList, insertIndex, shortcutParentShortcut, shortcutParentWindow, moved, create);
    }
  }
}
