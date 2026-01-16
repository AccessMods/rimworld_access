using System.Collections.Generic;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Shared helper for shape preview functionality.
    /// Centralizes two-point selection logic used by ShapePlacementState, ZoneCreationState, and AreaPaintingState.
    /// </summary>
    public class ShapePreviewHelper
    {
        // State fields
        private IntVec3? firstCorner = null;
        private IntVec3? secondCorner = null;
        private List<IntVec3> previewCells = new List<IntVec3>();
        private ShapeType currentShape = ShapeType.FilledRectangle;

        // Sound feedback tracking
        private int lastCellCount = 0;
        private float lastDragRealTime = 0f;

        // Properties
        public IntVec3? FirstCorner => firstCorner;
        public IntVec3? SecondCorner => secondCorner;
        public IReadOnlyList<IntVec3> PreviewCells => previewCells;
        public ShapeType CurrentShape => currentShape;
        public bool HasFirstCorner => firstCorner.HasValue;
        public bool IsInPreviewMode => firstCorner.HasValue && secondCorner.HasValue;

        public void SetCurrentShape(ShapeType shape)
        {
            currentShape = shape;
        }

        public void SetFirstCorner(IntVec3 cell, string context = "")
        {
            firstCorner = cell;
            secondCorner = null;
            previewCells.Clear();
            lastCellCount = 0;
            lastDragRealTime = Time.realtimeSinceStartup;
            TolkHelper.Speak($"First corner, {cell.x}, {cell.z}");
            if (!string.IsNullOrEmpty(context))
                Log.Message($"{context}: First corner set at {cell}");
        }

        public void SetSecondCorner(IntVec3 cell, string context = "")
        {
            if (!firstCorner.HasValue)
            {
                Log.Warning($"{context}: SetSecondCorner called without first corner set");
                return;
            }

            secondCorner = cell;
            previewCells = ShapeHelper.CalculateCells(currentShape, firstCorner.Value, cell);

            var (width, height) = ShapeHelper.GetDimensions(firstCorner.Value, cell);
            int cellCount = previewCells.Count;

            TolkHelper.Speak($"Second corner, {width} by {height}, {cellCount} cells");
            if (!string.IsNullOrEmpty(context))
                Log.Message($"{context}: Second corner at {cell}. {width}x{height}, {cellCount} cells");
        }

        public void UpdatePreview(IntVec3 cursor)
        {
            if (!firstCorner.HasValue) return;

            secondCorner = cursor;
            previewCells = ShapeHelper.CalculateCells(currentShape, firstCorner.Value, cursor);

            int cellCount = previewCells.Count;

            if (cellCount != lastCellCount)
            {
                PlayDragSound();
                lastCellCount = cellCount;

                var (width, height) = ShapeHelper.GetDimensions(firstCorner.Value, cursor);

                // Always announce dimensions in W by H format
                TolkHelper.Speak($"{width} by {height}", SpeechPriority.Low);
            }
        }

        public List<IntVec3> ConfirmShape(string context = "")
        {
            if (!IsInPreviewMode)
            {
                TolkHelper.Speak("No shape to confirm");
                return new List<IntVec3>();
            }

            var confirmedCells = new List<IntVec3>(previewCells);
            var (width, height) = ShapeHelper.GetDimensions(firstCorner.Value, secondCorner.Value);

            TolkHelper.Speak($"{width} by {height}, {confirmedCells.Count} cells confirmed");
            if (!string.IsNullOrEmpty(context))
                Log.Message($"{context}: Confirmed {confirmedCells.Count} cells");

            // Reset for next selection
            Reset();

            return confirmedCells;
        }

        public void Cancel()
        {
            if (!HasFirstCorner) return;

            Reset();
            TolkHelper.Speak("Shape cancelled");
        }

        public void Reset()
        {
            firstCorner = null;
            secondCorner = null;
            previewCells.Clear();
            lastCellCount = 0;
            lastDragRealTime = Time.realtimeSinceStartup;
            // Keep currentShape as is
        }

        public void FullReset()
        {
            Reset();
            currentShape = ShapeType.FilledRectangle;
        }

        private void PlayDragSound()
        {
            SoundInfo info = SoundInfo.OnCamera();
            info.SetParameter("TimeSinceDrag", Time.realtimeSinceStartup - lastDragRealTime);
            SoundDefOf.Designate_DragStandard_Changed.PlayOneShot(info);
            lastDragRealTime = Time.realtimeSinceStartup;
        }
    }
}
