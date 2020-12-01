﻿using System;
using System.Collections.Generic;
using MainScreen.StationView;
using Models.Project;
using Services;
using Services.Serialization;
using Services.UndoRedo;
using Services.UndoRedo.Commands;
using Services.UndoRedo.Models;
using Shared;
using Shared.Toast;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainScreen.Sidebar.HierarchyView
{
    /// <summary>
    ///     Delegate to notify sequence view about skip to clicked
    /// </summary>
    public delegate void Notify();

    /// <summary>
    ///     Manage the behaviour of a hierarchy view item
    /// </summary>
    public class HierarchyItemController : MonoBehaviour
    {
        /// <summary>
        ///     True if the user is currently dragging an item
        /// </summary>
        public static bool Dragging;

        /// <summary>
        ///     True if the user wants to insert an item (otherwise it will be put above)
        /// </summary>
        private static bool _insertion;

        /// <summary>
        ///     The item on which the drag ended
        /// </summary>
        private static HierarchyItemController _dragItem;

        /// <summary>
        ///     The selected items before starting a drag
        /// </summary>
        private static List<HierarchyItemController> _selectedItems = new List<HierarchyItemController>();

        /// <summary>
        ///     The colors for the item
        /// </summary>
        public Color highlightedColor, normalColor;

        /// <summary>
        ///     The hierarchy view controller
        /// </summary>
        public HierarchyViewController hierarchyViewController;

        /// <summary>
        ///     The text view in which the name is shown
        /// </summary>
        public TextMeshProUGUI nameText;

        /// <summary>
        ///     The input field for renaming an item
        /// </summary>
        public TMP_InputField nameInput;

        /// <summary>
        ///     The matching game objects for the name label and input
        /// </summary>
        public GameObject nameTextObject, nameInputObject;

        /// <summary>
        ///     The rect transform of the item's content
        /// </summary>
        public RectTransform itemContent;

        /// <summary>
        ///     The expand button with its logos
        /// </summary>
        public GameObject expandButton, expandDown, expandRight, fusion;

        /// <summary>
        ///     The button for showing a station
        /// </summary>
        public GameObject showStation;

        /// <summary>
        ///     Visualizes current item in the sequence view
        /// </summary>
        public GameObject itemActive;

        /// <summary>
        ///     The controller of the station view
        /// </summary>
        public StationController stationController;

        /// <summary>
        ///     The container of the item which contains all children
        /// </summary>
        public GameObject childrenContainer;

        /// <summary>
        ///     The context menu controller
        /// </summary>
        public ContextMenuController contextMenu;

        /// <summary>
        ///     The indicators for moving or putting an item
        /// </summary>
        public GameObject movingIndicator;

        /// <summary>
        ///     The background of the item
        /// </summary>
        public Image background;

        /// <summary>
        ///     The text for the preview of a list drag
        /// </summary>
        public TextMeshProUGUI dragPreviewText;

        /// <summary>
        ///     The preview object for a list drag
        /// </summary>
        public GameObject dragPreview;

        /// <summary>
        ///     The toast controller
        /// </summary>
        public ToastController toast;

        /// <summary>
        ///     The scroll view
        /// </summary>
        public ScrollRect scrollRect;

        /// <summary>
        ///     The item of the actual model
        /// </summary>
        [HideInInspector] public GameObject item;

        /// <summary>
        ///     The item of the actual model
        /// </summary>
        [HideInInspector] public ItemInfoController itemInfo;

        /// <summary>
        ///     The camera controller
        /// </summary>
        public CameraController cameraController;

        /// <summary>
        ///     The click detector instance
        /// </summary>
        public DoubleClickDetector clickDetector;

        /// <summary>
        ///     The text colors for visible and invisible items
        /// </summary>
        public Color visibleColor, invisibleColor;

        /// <summary>
        ///     The root element of the hierarchy view
        /// </summary>
        public RectTransform hierarchyView;

        /// <summary>
        ///     The project manager
        /// </summary>
        private readonly ProjectManager _projectManager = ProjectManager.Instance;

        /// <summary>
        ///     The undo redo service
        /// </summary>
        private readonly UndoService _undoService = UndoService.Instance;

        /// <summary>
        ///     True if the item was actually clicked
        /// </summary>
        private bool _clicked;

        /// <summary>
        ///     True if the child elements are expanded in the hierarchy view
        /// </summary>
        private bool _isExpanded = true;

        /// <summary>
        ///     True if the hierarchy view needs to be updated
        /// </summary>
        private bool _updateHierarchy;

        /// <summary>
        ///     True if the item has children
        /// </summary>
        private bool HasChildren => item.transform.childCount > 0;

        /// <summary>
        ///     True if the item is a station
        /// </summary>
        public bool IsStation => itemInfo.ItemInfo.isGroup && transform.parent == hierarchyView;

        /// <summary>
        ///     Late update of the UI
        /// </summary>
        private void LateUpdate()
        {
            // force update of the hierarchy view if the item expansion changed
            if (_updateHierarchy)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hierarchyView);

            clickDetector.CheckForSecondClick();
        }

        /// <summary>
        ///     Event to notify sequence view skip to was clicked
        /// </summary>
        public event Notify SkipToClicked;

        /// <summary>
        ///     Initialize the hierarchy item
        /// </summary>
        /// <param name="modelItem">The item of the actual model</param>
        /// <param name="indentionDepth">Depth of indentation inside the listview</param>
        public void Initialize(GameObject modelItem, float indentionDepth)
        {
            // Save the actual item
            item = modelItem;
            itemInfo = item.GetComponent<ItemInfoController>();

            // set the name of the item
            nameText.text = itemInfo.ItemInfo.displayName;

            // indent the item
            IndentItem(indentionDepth);

            // Show the item
            ShowItem(true);

            // Add the double click detector
            clickDetector.DoubleClickOccured += () =>
            {
                // Focus on component group only when there are children in group
                if (item.transform.childCount > 0)
                    cameraController.ZoomOnObject(item);

                // Focus on single component and make sure we have no empty group!
                else if (itemInfo.ItemInfo.isGroup == false)
                    cameraController.UpdateCameraFocus(item);
            };

            // Update the button
            UpdateVisuals();
        }

        /// <summary>
        ///     Change the text color of an item
        /// </summary>
        /// <param name="show">True if the item should be shown</param>
        public void ShowItem(bool show)
        {
            nameText.color = show ? visibleColor : invisibleColor;
            if (item != null) item.SetActive(show);
        }

        /// <summary>
        ///     Indent the item by a given depth
        /// </summary>
        /// <param name="indentionDepth">Depth of indentation inside the listview</param>
        public void IndentItem(float indentionDepth)
        {
            itemContent.offsetMin = new Vector2(indentionDepth, 0);
        }

        /// <summary>
        ///     Return the indention depth of the item
        /// </summary>
        /// <returns>The indention depth</returns>
        public float GetIndention()
        {
            return itemContent.offsetMin.x;
        }

        /// <summary>
        ///     Expand the item's content
        /// </summary>
        public void ExpandItem()
        {
            ExpandItem(!_isExpanded);
        }

        /// <summary>
        ///     Expand the item's content
        /// </summary>
        /// <param name="expand">True if the item shall be expanded</param>
        public void ExpandItem(bool expand)
        {
            if (HasChildren)
            {
                childrenContainer.SetActive(expand);
                _isExpanded = expand;
                _updateHierarchy = true;
            }

            UpdateVisuals();
        }

        /// <summary>
        ///     OnClick Method for the Selection of an item
        /// </summary>
        private void SelectItem()
        {
            // Item Selection if left control is used
            if (Input.GetKey(KeyCode.LeftControl))
                hierarchyViewController.ClickItem(this, KeyCode.LeftControl);

            // Item selection if the left shift key is used
            else if (Input.GetKey(KeyCode.LeftShift))
                hierarchyViewController.ClickItem(this, KeyCode.LeftShift);

            // Item Selection if No modifier is used
            else
                hierarchyViewController.ClickItem(this, KeyCode.None);
        }

        /// <summary>
        ///     Update the visuals of the item
        /// </summary>
        public void UpdateVisuals()
        {
            // Check if the item is fused
            var fused = itemInfo.ItemInfo.isFused;

            // Enable/Disable the button
            expandButton.SetActive(HasChildren || fused);

            // Update the logos if necessary
            expandDown.SetActive(_isExpanded && !fused);
            expandRight.SetActive(!_isExpanded && !fused);
            fusion.SetActive(fused);

            // Show/Hide the station button
            showStation.SetActive(IsStation);
        }

        /// <summary>
        ///     Shows/hide the visualisation of an currently active item in the sequence view
        /// </summary>
        /// <param name="isActive"></param>
        public void SetItemActive(bool isActive)
        {
            // Skip if item is a station
            if (IsStation) return;

            // Show/hide dot icon
            itemActive.SetActive(isActive);
        }


        /// <summary>
        ///     Show a station in the station view
        /// </summary>
        public void ShowStation()
        {
            stationController.ShowStation(this);
        }

        /// <summary>
        ///     Handle clicks on the item
        /// </summary>
        /// <param name="data">The event data</param>
        public void ItemClick(BaseEventData data)
        {
            // Set the clicked flag
            _clicked = true;

            // Select the item 
            var pointerData = (PointerEventData) data;
            if (!hierarchyViewController.IsSelected(this))
            {
                SelectItem();
                _clicked = false;
            }

            // Check what type of click happened
            if (pointerData.button == PointerEventData.InputButton.Left) clickDetector.Click();
        }

        /// <summary>
        ///     Handle click release
        /// </summary>
        /// <param name="data">The event data</param>
        public void ClickRelease(BaseEventData data)
        {
            var pointerData = (PointerEventData) data;
            switch (pointerData.button)
            {
                case PointerEventData.InputButton.Left when _clicked:
                    SelectItem();
                    break;
                case PointerEventData.InputButton.Right:
                    ShowContextMenu();
                    break;
            }

            _clicked = false;
        }

        /// <summary>
        ///     Open the context menu on right click
        /// </summary>
        private void ShowContextMenu()
        {
            var entries = new List<ContextMenuController.Item>
            {
                new ContextMenuController.Item {Icon = contextMenu.edit, Name = "Rename", Action = RenameItem}
            };

            var visible = item.activeSelf;
            entries.Add(
                new ContextMenuController.Item
                {
                    Icon = visible ? contextMenu.hide : contextMenu.show,
                    Name = visible ? "Hide Item" : "Show Item",
                    Action = () => ShowItem(!visible)
                }
            );

            var isGroup = itemInfo.ItemInfo.isGroup;
            if (isGroup)
                entries.Add(
                    new ContextMenuController.Item
                    {
                        Icon = contextMenu.show,
                        Name = "Show All",
                        Action = ShowGroup
                    }
                );

            if (hierarchyViewController.SelectedItems.Contains(this))
                entries.Add(
                    new ContextMenuController.Item
                    {
                        Icon = contextMenu.folder,
                        Name = "Group Selected",
                        Action = MoveToNewGroup
                    }
                );

            if (isGroup)
            {
                entries.Add(
                    new ContextMenuController.Item
                    {
                        Icon = itemInfo.ItemInfo.isFused ? contextMenu.defuse : contextMenu.fuse,
                        Name = itemInfo.ItemInfo.isFused ? "Split Group" : "Fuse Group",
                        Action = FuseGroup
                    }
                );

                entries.Add(
                    new ContextMenuController.Item
                    {
                        Icon = contextMenu.add,
                        Name = "Add Group",
                        Action = AddGroup
                    }
                );

                entries.Add(
                    new ContextMenuController.Item
                    {
                        Icon = contextMenu.delete,
                        Name = "Delete",
                        Action = DeleteGroup
                    }
                );
            }

            if (stationController.IsOpen)
                entries.Add(
                    new ContextMenuController.Item
                    {
                        Icon = contextMenu.skipTo,
                        Name = "Skip To",
                        Action = SequenceViewSkipToItem
                    }
                );

            contextMenu.Show(entries);
        }

        /// <summary>
        ///     Skips to the selected item in the sequence view
        /// </summary>
        private void SequenceViewSkipToItem()
        {
            // Skip if station view not open
            if (!stationController.IsOpen) return;

            // Invoke event
            //SkipToClicked?.Invoke();

            throw new NotImplementedException();
        }

        /// <summary>
        ///     Start a renaming action
        /// </summary>
        private void RenameItem()
        {
            nameInput.text = nameText.text;
            showStation.SetActive(false);
            nameInputObject.SetActive(true);
            nameInput.Select();
            nameTextObject.SetActive(false);
        }

        /// <summary>
        ///     Fuse a group
        /// </summary>
        private void FuseGroup()
        {
            itemInfo.ItemInfo.isFused = !itemInfo.ItemInfo.isFused;
            UpdateVisuals();
        }

        /// <summary>
        ///     Delete a group
        /// </summary>
        private void DeleteGroup()
        {
            //Check if the group isn't empty
            if (item.transform.childCount > 0)
                toast.Error(Toast.Short, "Only empty groups can be deleted!");
            else
                // Add the new creation command to the undo redo service
                _undoService.AddCommand(new CreateCommand(false, new ItemState(this)));
        }

        /// <summary>
        ///     Show a whole group
        /// </summary>
        private void ShowGroup()
        {
            ShowItem(true);
            Utility.ToggleVisibility(childrenContainer.transform, true);
        }

        /// <summary>
        ///     Add new group
        /// </summary>
        private void AddGroup()
        {
            // Save the item state
            var state = new ItemState(
                _projectManager.GetNextGroupID(),
                "Group",
                item.name,
                ItemState.Last
            );

            // Add the new action to the undo redo service
            _undoService.AddCommand(new CreateCommand(true, state));
        }

        /// <summary>
        ///     Create a new group and move the items into the group
        /// </summary>
        private void MoveToNewGroup()
        {
            // Create the group
            var state = new ItemState(
                _projectManager.GetNextGroupID(),
                "Group",
                item.transform.parent.name,
                Utility.GetNeighbourID(item.transform)
            );
            _undoService.AddCommand(new CreateCommand(true, state));

            // Move the items
            _dragItem =
                Utility.FindChild(hierarchyView.transform, state.ID).GetComponent<HierarchyItemController>();
            _insertion = true;
            _selectedItems = hierarchyViewController.GetSelectedItems();
            InsertItems();
        }

        /// <summary>
        ///     Cancel a rename action
        /// </summary>
        public void CancelRenaming()
        {
            nameInputObject.SetActive(false);
            nameTextObject.SetActive(true);
        }

        /// <summary>
        ///     Apply a rename action
        /// </summary>
        public void ApplyRenaming()
        {
            // Check if there's a name given
            var newName = nameInput.text;
            if (newName == "")
            {
                toast.Error(Toast.Short, "Name cannot be empty!");
                return;
            }

            // Hide the input field an show the name field
            nameInputObject.SetActive(false);
            nameTextObject.SetActive(true);
            showStation.SetActive(IsStation);

            // Add the new action to the undo redo service
            _undoService.AddCommand(new RenameCommand(item.name, nameText.text, newName));
        }

        /// <summary>
        ///     Start dragging one or multiple items
        /// </summary>
        /// <param name="data">Event data</param>
        public void StartDraggingItem(BaseEventData data)
        {
            // Set the clicked flag to false
            _clicked = false;

            // Get the selected items
            _selectedItems = hierarchyViewController.GetSelectedItems();
            if (_selectedItems.Count == 0 || !_selectedItems.Contains(this)) return;

            // Show which items are dragged
            var firstName = itemInfo.ItemInfo.displayName;
            dragPreviewText.text = _selectedItems.Count > 1 ? "Multiple Items" : firstName;
            dragPreview.transform.position = ((PointerEventData) data).position;
            dragPreview.SetActive(true);
            Dragging = true;
        }

        /// <summary>
        ///     Drag one or multiple items
        /// </summary>
        /// <param name="data">Event data</param>
        public void DragItem(BaseEventData data)
        {
            // Get the pointer position
            Vector3 position = ((PointerEventData) data).position;
            dragPreview.transform.position = new Vector3(position.x + 5, position.y - 5, 0);
        }

        /// <summary>
        ///     Stop dragging
        /// </summary>
        /// <param name="data">Event data</param>
        public void StopDraggingItem(BaseEventData data)
        {
            // Reset the drag event
            dragPreview.SetActive(false);
            Dragging = false;

            // Insert the items (Only if the dragged item was selected)
            if (_selectedItems.Count != 0 && hierarchyViewController.IsSelected(this)) InsertItems();
        }

        /// <summary>
        ///     Insert the currently selected items into a group
        /// </summary>
        private void InsertItems()
        {
            // Hide the insertion area of the station view
            stationController.HideInsertionArea();

            // Check if the drag leads to a change
            if (_dragItem == null) return;

            // Get the new parent and the new neighbour id
            var parent = _insertion ? _dragItem.gameObject.name : _dragItem.item.transform.parent.name;
            var neighbourID = _insertion ? ItemState.Last : Utility.GetNeighbourID(_dragItem.transform);

            // Create the item states
            List<ItemState> oldStates = new List<ItemState>(), newStates = new List<ItemState>();
            for (var i = 0; i < _selectedItems.Count; i++)
            {
                // Check whether a group is dragged into itself
                if (Utility.IsParent(_dragItem.item.transform, _selectedItems[i].item.name))
                {
                    Debug.Log("Test");
                    toast.Error(Toast.Short, "Cannot make a group a child of its own!");
                    return;
                }

                // Create the old state
                oldStates.Add(new ItemState(_selectedItems[i]));

                // Create the new state
                newStates.Add(
                    new ItemState(oldStates[i]) {ParentID = parent, NeighbourID = neighbourID}
                );
                if (neighbourID != ItemState.Last) neighbourID = newStates[i].ID;
            }

            // Add the new action to the undo redo service
            _undoService.AddCommand(new MoveCommand(oldStates, newStates));

            // Update the station view
            stationController.UpdateStation();
        }

        /// <summary>
        ///     Put an item above this item
        /// </summary>
        /// <param name="data">Event data</param>
        public void PutAbove(BaseEventData data)
        {
            // Change the color
            var selected = hierarchyViewController.IsSelected(this);
            background.color = Dragging && !selected ? normalColor : highlightedColor;


            // Show the moving indicator
            movingIndicator.SetActive(Dragging && !selected);

            // Save the item and the action
            _insertion = false;
            _dragItem = selected ? null : this;
        }

        /// <summary>
        ///     Stop putting an item above this item
        /// </summary>
        /// <param name="data">Event data</param>
        public void StopPuttingAbove(BaseEventData data)
        {
            movingIndicator.SetActive(false);
            if (!hierarchyViewController.IsSelected(this)) background.color = normalColor;
            _dragItem = null;
        }

        /// <summary>
        ///     Insert an item into this item
        ///     (only works with if this item is a group)
        /// </summary>
        /// <param name="data">Event data</param>
        public void InsertItem(BaseEventData data)
        {
            // Change the color
            var isGroup = itemInfo.ItemInfo.isGroup;
            var selected = hierarchyViewController.IsSelected(this);
            background.color = Dragging && !isGroup && !selected ? normalColor : highlightedColor;

            // Save the item and the action if item is compatible
            _insertion = true;
            _dragItem = isGroup ? this : null;
        }

        /// <summary>
        ///     Stop inserting an item into this item
        /// </summary>
        /// <param name="data">Event data</param>
        public void StopInsertingItem(BaseEventData data)
        {
            _dragItem = null;
            if (!hierarchyViewController.IsSelected(this)) background.color = normalColor;
        }

        /// <summary>
        ///     Forward the scroll data
        /// </summary>
        /// <param name="data">Event data</param>
        public void OnScroll(BaseEventData data)
        {
            scrollRect.OnScroll((PointerEventData) data);
        }
    }
}