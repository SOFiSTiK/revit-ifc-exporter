﻿//
// BIM IFC library: this library works with Autodesk(R) Revit(R) to export IFC files containing model geometry.
// Copyright (C) 2012  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB.Mechanical;
using Revit.IFC.Export.Exporter;
using Revit.IFC.Export.Exporter.PropertySet;
using Revit.IFC.Export.Toolkit;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;

namespace Revit.IFC.Export.Utility
{
   /// <summary>
   /// Provides general utility methods for IFC export.
   /// </summary>
   public class ExporterUtil
   {
      private static ProjectPosition GetSafeProjectPosition(Document doc)
      {
         ProjectLocation projLoc = ExporterCacheManager.SelectedSiteProjectLocation;
         try
         {
            return projLoc.GetProjectPosition(XYZ.Zero);
         }
         catch
         {
            return null;
         }
      }
      private static void Union<T>(ref IList<T> lList, IList<T> rList)
      {
         if (rList == null || rList.Count() == 0)
            return;

         if (lList.Count() == 0)
            lList = rList;
         else
            lList = lList.Union(rList).ToList();
      }

      /// <summary>
      /// Get the "GlobalId" value for a handle, or an empty string if it doesn't exist.
      /// </summary>
      /// <param name="handle">The IFC entity.</param>
      /// <returns>The "GlobalId" value for a handle, or an empty string if it doesn't exist.</returns>
      public static string GetGlobalId(IFCAnyHandle handle)
      {
         try
         {
            return IFCAnyHandleUtil.GetStringAttribute(handle, "GlobalId");
         }
         catch
         {
            return String.Empty;
         }
      }

      /// <summary>
      /// Set the "GlobalId" value for a handle if it exists.
      /// </summary>
      /// <param name="handle">The IFC entity.</param>
      /// <param name="guid">The GUID value.</param>
      public static void SetGlobalId(IFCAnyHandle handle, string guid, Element element = null)
      {
         try
         {
            guid = GUIDUtil.RegisterGUID(element, guid);
            IFCAnyHandleUtil.SetAttribute(handle, "GlobalId", guid);
         }
         catch
         {
         }
         
      }

      /// <summary>
      /// Gets the angle associated with the project position for a particular document.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="angle">The angle, or 0.0 if it can't be generated.</param>
      /// <returns>True if the angle is found, false if it can't be determined.</returns>
      public static bool GetSafeProjectPositionAngle(Document doc, out double angle)
      {
         angle = 0.0;
         ProjectPosition projPos = GetSafeProjectPosition(doc);
         if (projPos == null)
            return false;

         angle = projPos.Angle;
         return true;
      }

      /// <summary>
      /// Gets the elevation associated with the project position for a particular document. 
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="elevation">The elevation, or 0.0 if it can't be generated.</param>
      /// <returns>True if the elevation is found, false if it can't be determined.</returns>
      public static bool GetSafeProjectPositionElevation(Document doc, out double elevation)
      {
         elevation = 0.0;
         ProjectPosition projPos = GetSafeProjectPosition(doc);
         if (projPos == null)
            return false;

         elevation = projPos.Elevation;
         return true;
      }

      /// <summary>
      /// Determines if the Exception is local to the element, or if export should be aborted.
      /// </summary>
      /// <param name="document">The document.</param>
      /// <param name="ex">The unexpected exception.</param>
      public static bool IsFatalException(Document document, Exception exception)
      {
         string msg = exception.ToString();
         if (msg.Contains("Error in allocating memory"))
         {
            if (document == null)
               return true;

            FailureMessage fm = new FailureMessage(BuiltInFailures.ExportFailures.IFCFatalToolkitExportError);
            document.PostFailure(fm);
            return true;
         }
         return false;
      }

      /// <summary>
      /// Relates one object to another. 
      /// </summary>
      /// <param name="exporterIFC">
      /// The ExporterIFC object.
      /// </param>
      /// <param name="relatingObject">
      /// The relating object.
      /// </param>
      /// <param name="relatedObject">
      /// The related object.
      /// </param>
      public static void RelateObject(ExporterIFC exporterIFC, IFCAnyHandle relatingObject, IFCAnyHandle relatedObject)
      {
         HashSet<IFCAnyHandle> relatedObjects = new HashSet<IFCAnyHandle>();
         relatedObjects.Add(relatedObject);
         RelateObjects(exporterIFC, null, relatingObject, relatedObjects);
      }

      /// <summary>
      /// Relates one object to a collection of others. 
      /// </summary>
      /// <param name="exporterIFC">
      /// The ExporterIFC object.
      /// </param>
      /// <param name="optionalGUID">
      /// A GUID value, or null to generate a random GUID.
      /// </param>
      /// <param name="relatingObject">
      /// The relating object.
      /// </param>
      /// <param name="relatedObjects">
      /// The related objects.
      /// </param>
      public static void RelateObjects(ExporterIFC exporterIFC, string optionalGUID, IFCAnyHandle relatingObject, ICollection<IFCAnyHandle> relatedObjects)
      {
         string guid = optionalGUID ?? GUIDUtil.GenerateIFCGuidFrom(
            GUIDUtil.CreateGUIDString(IFCEntityType.IfcRelAggregates, relatingObject));
         IFCInstanceExporter.CreateRelAggregates(exporterIFC.GetFile(), guid, ExporterCacheManager.OwnerHistoryHandle, null, null, relatingObject, new HashSet<IFCAnyHandle>(relatedObjects));
      }

      /// <summary>
      /// Creates IfcAxis2Placement3D object.
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="origin">
      /// The origin.
      /// </param>
      /// <param name="zDirection">
      /// The Z direction.
      /// </param>
      /// <param name="xDirection">
      /// The X direction.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateAxis(IFCFile file, XYZ origin, XYZ zDirection, XYZ xDirection)
      {
         IFCAnyHandle direction = null;
         IFCAnyHandle refDirection = null;
         IFCAnyHandle location = null;

         if (origin != null)
         {
            IList<double> measure = new List<double>();
            measure.Add(origin.X); measure.Add(origin.Y); measure.Add(origin.Z);
            location = CreateCartesianPoint(file, measure);
         }
         else
         {
            location = ExporterCacheManager.Global3DOriginHandle;
         }

         bool exportzDirectionAndxDirection = (zDirection != null && xDirection != null && (!MathUtil.IsAlmostEqual(zDirection[2], 1.0) || !MathUtil.IsAlmostEqual(xDirection[0], 1.0)));

         if (exportzDirectionAndxDirection)
         {
            IList<double> axisPts = new List<double>();
            axisPts.Add(zDirection.X); axisPts.Add(zDirection.Y); axisPts.Add(zDirection.Z);
            direction = CreateDirection(file, axisPts);
         }

         if (exportzDirectionAndxDirection)
         {
            IList<double> axisPts = new List<double>();
            axisPts.Add(xDirection.X); axisPts.Add(xDirection.Y); axisPts.Add(xDirection.Z);
            refDirection = CreateDirection(file, axisPts);
         }

         return IFCInstanceExporter.CreateAxis2Placement3D(file, location, direction, refDirection);
      }

      /// <summary>
      /// Creates IfcDirection object.
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="realList">
      /// The list of doubles to create the direction.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateDirection(IFCFile file, IList<double> realList)
      {
         IList<double> cleanList = new List<double>();

         foreach (double measure in realList)
         {
            double ceilMeasure = Math.Ceiling(measure);
            double floorMeasure = Math.Floor(measure);

            if (MathUtil.IsAlmostEqual(measure, ceilMeasure))
               cleanList.Add(ceilMeasure);
            else if (MathUtil.IsAlmostEqual(measure, floorMeasure))
               cleanList.Add(floorMeasure);
            else
               cleanList.Add(measure);
         }

         int sz = realList.Count;

         if (sz == 3)
         {
            for (int ii = 0; ii < 3; ii++)
            {
               if (MathUtil.IsAlmostEqual(cleanList[ii], 1.0))
               {
                  if (!MathUtil.IsAlmostZero(cleanList[(ii + 1) % 3]) || !MathUtil.IsAlmostZero(cleanList[(ii + 2) % 3]))
                     break;
                  return ExporterIFCUtils.GetGlobal3DDirectionHandles(true)[ii];
               }
               else if (MathUtil.IsAlmostEqual(cleanList[ii], -1.0))
               {
                  if (!MathUtil.IsAlmostZero(cleanList[(ii + 1) % 3]) || !MathUtil.IsAlmostZero(cleanList[(ii + 2) % 3]))
                     break;
                  return ExporterIFCUtils.GetGlobal3DDirectionHandles(false)[ii];
               }
            }
         }
         else if (sz == 2)
         {
            for (int ii = 0; ii < 2; ii++)
            {
               if (MathUtil.IsAlmostEqual(cleanList[ii], 1.0))
               {
                  if (!MathUtil.IsAlmostZero(cleanList[1 - ii]))
                     break;
                  return ExporterIFCUtils.GetGlobal2DDirectionHandles(true)[ii];
               }
               else if (MathUtil.IsAlmostEqual(cleanList[ii], -1.0))
               {
                  if (!MathUtil.IsAlmostZero(cleanList[1 - ii]))
                     break;
                  return ExporterIFCUtils.GetGlobal2DDirectionHandles(false)[ii];
               }
            }
         }

         IFCAnyHandle directionHandle = IFCInstanceExporter.CreateDirection(file, cleanList);
         return directionHandle;
      }

      /// <summary>
      /// Creates IfcDirection object.
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="direction">
      /// The direction.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateDirection(IFCFile file, XYZ direction)
      {
         IList<double> measure = new List<double>();
         measure.Add(direction.X);
         measure.Add(direction.Y);
         measure.Add(direction.Z);
         return CreateDirection(file, measure);
      }

      /// <summary>
      /// Creates IfcVector object.
      /// </summary>
      /// <param name="file">The IFC file.</param>
      /// <param name="directionXYZ">The XYZ value represention the vector direction.</param>
      /// <returns>The IfcVector handle.</returns>
      public static IFCAnyHandle CreateVector(IFCFile file, XYZ directionXYZ, double length)
      {
         IFCAnyHandle direction = CreateDirection(file, directionXYZ);
         return IFCInstanceExporter.CreateVector(file, direction, length);
      }

      /// <summary>
      /// Creates IfcCartesianPoint object from a 2D point.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="point">The point</param>
      /// <returns>The IfcCartesianPoint handle.</returns>
      public static IFCAnyHandle CreateCartesianPoint(IFCFile file, UV point)
      {
         if (point == null)
            throw new ArgumentNullException("point");

         List<double> points = new List<double>();
         points.Add(point.U);
         points.Add(point.V);

         return CreateCartesianPoint(file, points);
      }

      /// <summary>
      /// Creates IfcCartesianPoint object from a 3D point.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="point">The point</param>
      /// <returns>The IfcCartesianPoint handle.</returns>
      public static IFCAnyHandle CreateCartesianPoint(IFCFile file, XYZ point)
      {
         if (point == null)
            throw new ArgumentNullException("point");

         List<double> points = new List<double>();
         points.Add(point.X);
         points.Add(point.Y);
         points.Add(point.Z);

         return CreateCartesianPoint(file, points);
      }

      /// <summary>
      /// Creates IfcCartesianPoint object.
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="measure">
      /// The list of doubles to create the Cartesian point.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateCartesianPoint(IFCFile file, IList<double> measure)
      {
         IList<double> cleanMeasure = new List<double>();
         foreach (double value in measure)
         {
            double ceilMeasure = Math.Ceiling(value);
            double floorMeasure = Math.Floor(value);

            if (MathUtil.IsAlmostEqual(value, ceilMeasure))
               cleanMeasure.Add(ceilMeasure);
            else if (MathUtil.IsAlmostEqual(value, floorMeasure))
               cleanMeasure.Add(floorMeasure);
            else
               cleanMeasure.Add(value);
         }

         if (MathUtil.IsAlmostZero(cleanMeasure[0]) && MathUtil.IsAlmostZero(cleanMeasure[1]))
         {
            if (measure.Count == 2)
            {
               return ExporterIFCUtils.GetGlobal2DOriginHandle();
            }
            if (measure.Count == 3 && MathUtil.IsAlmostZero(cleanMeasure[2]))
            {
               return ExporterCacheManager.Global3DOriginHandle;
            }

         }

         IFCAnyHandle pointHandle = IFCInstanceExporter.CreateCartesianPoint(file, cleanMeasure);

         return pointHandle;
      }

      /// <summary>
      /// Creates an IfcAxis2Placement3D object.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="location">The origin. If null, it will use the global origin handle.</param>
      /// <param name="axis">The Z direction.</param>
      /// <param name="refDirection">The X direction.</param>
      /// <returns>the handle.</returns>
      public static IFCAnyHandle CreateAxis2Placement3D(IFCFile file, XYZ location, XYZ axis, XYZ refDirection)
      {
         IFCAnyHandle locationHandle = null;
         if (location != null)
         {
            List<double> measure = new List<double>();
            measure.Add(location.X);
            measure.Add(location.Y);
            measure.Add(location.Z);
            locationHandle = CreateCartesianPoint(file, measure);
         }
         else
         {
            locationHandle = ExporterCacheManager.Global3DOriginHandle;
         }


         bool exportDirAndRef = (axis != null && refDirection != null &&
             (!MathUtil.IsAlmostEqual(axis[2], 1.0) || !MathUtil.IsAlmostEqual(refDirection[0], 1.0)));

         if ((axis != null) ^ (refDirection != null))
         {
            exportDirAndRef = false;
         }

         IFCAnyHandle axisHandle = null;
         if (exportDirAndRef)
         {
            List<double> measure = new List<double>();
            measure.Add(axis.X);
            measure.Add(axis.Y);
            measure.Add(axis.Z);
            axisHandle = CreateDirection(file, measure);
         }

         IFCAnyHandle refDirectionHandle = null;
         if (exportDirAndRef)
         {
            List<double> measure = new List<double>();
            measure.Add(refDirection.X);
            measure.Add(refDirection.Y);
            measure.Add(refDirection.Z);
            refDirectionHandle = CreateDirection(file, measure);
         }

         return IFCInstanceExporter.CreateAxis2Placement3D(file, locationHandle, axisHandle, refDirectionHandle);
      }

      /// <summary>
      /// Creates an IfcAxis2Placement3D object.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="location">The origin.</param>
      /// <returns>The handle.</returns>
      public static IFCAnyHandle CreateAxis2Placement3D(IFCFile file, XYZ location)
      {
         return CreateAxis2Placement3D(file, location, null, null);
      }

      /// <summary>
      /// Creates a default IfcAxis2Placement3D object.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <returns>The handle.</returns>
      public static IFCAnyHandle CreateAxis2Placement3D(IFCFile file)
      {
         return CreateAxis2Placement3D(file, null);
      }

      /// <summary>
      /// Creates IfcMappedItem object from an origin.
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="repMap">
      /// The handle to be mapped.
      /// </param>
      /// <param name="orig">
      /// The orig for mapping transformation.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateDefaultMappedItem(IFCFile file, IFCAnyHandle repMap, XYZ orig)
      {
         if (MathUtil.IsAlmostZero(orig.X) && MathUtil.IsAlmostZero(orig.Y) && MathUtil.IsAlmostZero(orig.Z))
            return CreateDefaultMappedItem(file, repMap);

         IFCAnyHandle origin = CreateCartesianPoint(file, orig);
         double scale = 1.0;
         IFCAnyHandle mappingTarget =
            IFCInstanceExporter.CreateCartesianTransformationOperator3D(file, null, null, origin, scale, null);
         return IFCInstanceExporter.CreateMappedItem(file, repMap, mappingTarget);
      }

      /// <summary>
      /// Creates IfcMappedItem object at (0,0,0).
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="repMap">
      /// The handle to be mapped.
      /// </param>
      /// <param name="orig">
      /// The orig for mapping transformation.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateDefaultMappedItem(IFCFile file, IFCAnyHandle repMap)
      {
         IFCAnyHandle transformHnd = ExporterCacheManager.GetDefaultCartesianTransformationOperator3D(file);
         return IFCInstanceExporter.CreateMappedItem(file, repMap, transformHnd);
      }

      /// <summary>
      /// Creates IfcMappedItem object from a transform
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="repMap">
      /// The handle to be mapped.
      /// </param>
      /// <param name="transform">
      /// The transform.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CreateMappedItemFromTransform(IFCFile file, IFCAnyHandle repMap, Transform transform)
      {
         IFCAnyHandle axis1 = CreateDirection(file, transform.BasisX);
         IFCAnyHandle axis2 = CreateDirection(file, transform.BasisY);
         IFCAnyHandle axis3 = CreateDirection(file, transform.BasisZ);
         IFCAnyHandle origin = CreateCartesianPoint(file, transform.Origin);
         double scale = 1.0;
         IFCAnyHandle mappingTarget =
            IFCInstanceExporter.CreateCartesianTransformationOperator3D(file, axis1, axis2, origin, scale, axis3);
         return IFCInstanceExporter.CreateMappedItem(file, repMap, mappingTarget);
      }

      /// <summary>
      /// Creates an IfcPolyLine from a list of UV points.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="polylinePts">This list of UV values.</param>
      /// <returns>An IfcPolyline handle.</returns>
      public static IFCAnyHandle CreatePolyline(IFCFile file, IList<UV> polylinePts)
      {
         int numPoints = polylinePts.Count;
         if (numPoints < 2)
            return null;

         bool closed = MathUtil.IsAlmostEqual(polylinePts[0], polylinePts[numPoints - 1]);
         if (closed)
         {
            if (numPoints == 2)
               return null;
            numPoints--;
         }

         IList<IFCAnyHandle> points = new List<IFCAnyHandle>();
         for (int ii = 0; ii < numPoints; ii++)
         {
            points.Add(CreateCartesianPoint(file, polylinePts[ii]));
         }
         if (closed)
            points.Add(points[0]);

         return IFCInstanceExporter.CreatePolyline(file, points);
      }

      /// <summary>
      /// Creates a copy of local placement object.
      /// </summary>
      /// <param name="file">
      /// The IFC file.
      /// </param>
      /// <param name="originalPlacement">
      /// The original placement object to be copied.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CopyLocalPlacement(IFCFile file, IFCAnyHandle originalPlacement)
      {
         IFCAnyHandle placementRelToOpt = GeometryUtil.GetPlacementRelToFromLocalPlacement(originalPlacement);
         IFCAnyHandle relativePlacement = GeometryUtil.GetRelativePlacementFromLocalPlacement(originalPlacement);
         return IFCInstanceExporter.CreateLocalPlacement(file, placementRelToOpt, relativePlacement);
      }

      /// <summary>
      /// Creates a new local placement object.
      /// </summary>
      /// <param name="file">The IFC file.</param>
      /// <param name="placementRelTo">The placement object.</param>
      /// <param name="relativePlacement">The relative placement. Null to create a identity relative placement.</param>
      /// <returns></returns>
      public static IFCAnyHandle CreateLocalPlacement(IFCFile file, IFCAnyHandle placementRelTo, IFCAnyHandle relativePlacement)
      {
         if (relativePlacement == null)
         {
            relativePlacement = ExporterUtil.CreateAxis2Placement3D(file);
         }
         return IFCInstanceExporter.CreateLocalPlacement(file, placementRelTo, relativePlacement);
      }

      /// <summary>
      /// Creates a new local placement object.
      /// </summary>
      /// <param name="file">The IFC file.</param>
      /// <param name="placementRelTo">The placement object.</param>
      /// <param name="location">The relative placement origin.</param>
      /// <param name="axis">The relative placement Z value.</param>
      /// <param name="refDirection">The relative placement X value.</param>
      /// <returns></returns>
      public static IFCAnyHandle CreateLocalPlacement(IFCFile file, IFCAnyHandle placementRelTo, XYZ location, XYZ axis, XYZ refDirection)
      {
         IFCAnyHandle relativePlacement = ExporterUtil.CreateAxis2Placement3D(file, location, axis, refDirection);
         return IFCInstanceExporter.CreateLocalPlacement(file, placementRelTo, relativePlacement);
      }

      public static IList<IFCAnyHandle> CopyRepresentations(ExporterIFC exporterIFC, Element element, ElementId catId, IFCAnyHandle origProductRepresentation)
      {
         IList<IFCAnyHandle> origReps = IFCAnyHandleUtil.GetRepresentations(origProductRepresentation);
         IList<IFCAnyHandle> newReps = new List<IFCAnyHandle>();
         IFCFile file = exporterIFC.GetFile();

         int num = origReps.Count;
         for (int ii = 0; ii < num; ii++)
         {
            IFCAnyHandle repHnd = origReps[ii];
            if (IFCAnyHandleUtil.IsTypeOf(repHnd, IFCEntityType.IfcShapeRepresentation))
            {
               IFCAnyHandle newRepHnd = RepresentationUtil.CreateShapeRepresentation(exporterIFC, element, catId,
                   IFCAnyHandleUtil.GetContextOfItems(repHnd),
                   IFCAnyHandleUtil.GetRepresentationIdentifier(repHnd), IFCAnyHandleUtil.GetRepresentationType(repHnd),
                   IFCAnyHandleUtil.GetItems(repHnd));
               newReps.Add(newRepHnd);
            }
            else
            {
               // May want to throw exception here.
               newReps.Add(repHnd);
            }
         }

         return newReps;
      }

      /// <summary>
      /// Creates a copy of a product definition shape.
      /// </summary>
      /// <param name="exporterIFC">
      /// The exporter.
      /// </param>
      /// <param name="origProductDefinitionShape">
      /// The original product definition shape to be copied.
      /// </param>
      /// <returns>
      /// The handle.
      /// </returns>
      public static IFCAnyHandle CopyProductDefinitionShape(ExporterIFC exporterIFC,
          Element elem,
          ElementId catId,
          IFCAnyHandle origProductDefinitionShape)
      {
         if (IFCAnyHandleUtil.IsNullOrHasNoValue(origProductDefinitionShape))
            return null;

         IList<IFCAnyHandle> representations = CopyRepresentations(exporterIFC, elem, catId, origProductDefinitionShape);

         IFCFile file = exporterIFC.GetFile();
         return IFCInstanceExporter.CreateProductDefinitionShape(file, IFCAnyHandleUtil.GetProductDefinitionShapeName(origProductDefinitionShape),
             IFCAnyHandleUtil.GetProductDefinitionShapeDescription(origProductDefinitionShape), representations);
      }

      private static string GetIFCClassNameFromExportTable(ExporterIFC exporterIFC, Element element, ElementId categoryId, int specialClassId)
      {
         if (element == null)
            return null;

         KeyValuePair<ElementId, int> key = new KeyValuePair<ElementId, int>(categoryId, specialClassId);
         string ifcClassName = null;
         if (!ExporterCacheManager.CategoryClassNameCache.TryGetValue(key, out ifcClassName))
         {
            ifcClassName = ExporterIFCUtils.GetIFCClassName(element, exporterIFC);
            ExporterCacheManager.CategoryClassNameCache[key] = ifcClassName;
         }

         return ifcClassName;
      }

      private static string GetIFCTypeFromExportTable(ExporterIFC exporterIFC, Element element, ElementId categoryId, int specialClassId)
      {
         if (element == null)
            return null;

         KeyValuePair<ElementId, int> key = new KeyValuePair<ElementId, int>(categoryId, specialClassId);
         string ifcType = null;
         if (!ExporterCacheManager.CategoryTypeCache.TryGetValue(key, out ifcType))
         {
            ifcType = ExporterIFCUtils.GetIFCType(element, exporterIFC);
            ExporterCacheManager.CategoryTypeCache[key] = ifcType;
         }

         return ifcType;
      }

      /// <summary>
      /// Get the IFC class name assigned in the export layers table for a category.  Cache values to avoid calls to internal code.
      /// </summary>
      /// <param name="exporterIFC">The exporterIFC class.</param>
      /// <param name="categoryId">The category id.</param>
      /// <returns>The entity name.</returns>
      public static string GetIFCClassNameFromExportTable(ExporterIFC exporterIFC, ElementId categoryId)
      {
         if (categoryId == ElementId.InvalidElementId)
            return null;

         KeyValuePair<ElementId, int> key = new KeyValuePair<ElementId, int>(categoryId, -1);
         string ifcClassName = null;
         if (!ExporterCacheManager.CategoryClassNameCache.TryGetValue(key, out ifcClassName))
         {
            string ifcClassAndTypeName = ExporterIFCUtils.GetIFCClassNameByCategory(categoryId, exporterIFC);
            string ifcTypeName = null;
            ExportEntityAndPredefinedType(ifcClassAndTypeName, out ifcClassName, out ifcTypeName);
            ExporterCacheManager.CategoryClassNameCache[key] = ifcClassName;

            // This actually represents an error in the export layers table, where the class name and type name
            // or jointly given as a class name.  This worked before, though, so for now we'll allow this case
            // to continue working.
            if (!string.IsNullOrEmpty(ifcTypeName) &&
               (!ExporterCacheManager.CategoryTypeCache.ContainsKey(key) ||
               string.IsNullOrEmpty(ExporterCacheManager.CategoryTypeCache[key])))
               ExporterCacheManager.CategoryTypeCache[key] = ifcTypeName;
         }

         return ifcClassName;
      }

      private static string GetIFCClassNameOrTypeForMass(ExporterIFC exporterIFC, Element element, ElementId categoryId, bool getClassName)
      {
         Options geomOptions = GeometryUtil.GetIFCExportGeometryOptions();
         GeometryElement geomElem = element.get_Geometry(geomOptions);
         if (geomElem == null)
            return null;

         SolidMeshGeometryInfo solidMeshCapsule = GeometryUtil.GetSplitSolidMeshGeometry(geomElem);
         IList<SolidInfo> solidInfos = solidMeshCapsule.GetSolidInfos();
         IList<Mesh> meshes = solidMeshCapsule.GetMeshes();

         ElementId overrideCatId = ElementId.InvalidElementId;
         bool initOverrideCatId = false;

         Document doc = element.Document;

         foreach (SolidInfo solidInfo in solidInfos)
         {
            if (!ProcessObjectForGStyle(doc, solidInfo.Solid, ref overrideCatId, ref initOverrideCatId))
               return null;
         }

         foreach (Mesh mesh in meshes)
         {
            if (!ProcessObjectForGStyle(doc, mesh, ref overrideCatId, ref initOverrideCatId))
               return null;
         }

         if (getClassName)
            return GetIFCClassNameFromExportTable(exporterIFC, overrideCatId);
         else
         {
            // At the moment, we don't have the right API to get the type from a categoryId instead of from an element from the category table.  As such, we are
            // going to hardwire this.  The only one that matters is OST_MassFloor.
            if (overrideCatId == new ElementId(BuiltInCategory.OST_MassFloor))
            {
               string className = GetIFCClassNameFromExportTable(exporterIFC, overrideCatId);
               if (string.Compare(className, "IfcSlab", true) == 0)
                  return "FLOOR";
               if (string.Compare(className, "IfcCovering", true) == 0)
                  return "FLOORING";
            }

            return null; // GetIFCTypeFromExportTable(exporterIFC, overrideCatId);
         }
      }

      private static string GetIFCClassNameOrTypeForWalls(ExporterIFC exporterIFC, Wall wall, ElementId categoryId, bool getClassName)
      {
         WallType wallType = wall.WallType;
         if (wallType == null)
            return null;

         int wallFunction;
         if (ParameterUtil.GetIntValueFromElement(wallType, BuiltInParameter.FUNCTION_PARAM, out wallFunction) != null)
         {
            if (getClassName)
               return GetIFCClassNameFromExportTable(exporterIFC, wall, categoryId, wallFunction);
            else
               return GetIFCTypeFromExportTable(exporterIFC, wall, categoryId, wallFunction);
         }

         return null;
      }

      private static bool ProcessObjectForGStyle(Document doc, GeometryObject geomObj, ref ElementId overrideCatId, ref bool initOverrideCatId)
      {
         GraphicsStyle gStyle = doc.GetElement(geomObj.GraphicsStyleId) as GraphicsStyle;
         if (gStyle == null)
            return true;

         if (gStyle.GraphicsStyleCategory == null)
            return true;

         ElementId currCatId = gStyle.GraphicsStyleCategory.Id;
         if (currCatId == ElementId.InvalidElementId)
            return true;

         if (!initOverrideCatId)
         {
            initOverrideCatId = true;
            overrideCatId = currCatId;
            return true;
         }

         if (currCatId != overrideCatId)
         {
            overrideCatId = ElementId.InvalidElementId;
            return false;
         }

         return true;
      }

      private static string GetIFCClassNameOrTypeFromSpecialEntry(ExporterIFC exporterIFC, Element element, ElementId categoryId, bool getClassName)
      {
         if (element == null)
            return null;

         // We do special checks for Wall and Massing categories.
         // For walls, we check if it is an interior or exterior wall.
         // For massing, we check the geometry.  If it is all in the same sub-category, we use that instead.
         if (categoryId == new ElementId(BuiltInCategory.OST_Walls))
         {
            if (element is Wall)
               return GetIFCClassNameOrTypeForWalls(exporterIFC, element as Wall, categoryId, getClassName);
         }
         else if (categoryId == new ElementId(BuiltInCategory.OST_Mass))
         {
            return GetIFCClassNameOrTypeForMass(exporterIFC, element, categoryId, getClassName);
         }

         return null;
      }

      /// <summary>
      /// Get the IFC class name assigned in the export layers table for a category.  Cache values to avoid calls to internal code.
      /// </summary>
      /// <param name="exporterIFC">The exporterIFC class.</param>
      /// <param name="element">The element.</param>
      /// <param name="categoryId">The returned category id.</param>
      /// <returns>The entity name.</returns>
      public static string GetIFCClassNameFromExportTable(ExporterIFC exporterIFC, Element element, out ElementId categoryId)
      {
         categoryId = ElementId.InvalidElementId;

         Category category = element.Category;
         if (category == null)
            return null;

         categoryId = category.Id;
         string specialEntry = GetIFCClassNameOrTypeFromSpecialEntry(exporterIFC, element, categoryId, true);
         if (specialEntry != null)
            return specialEntry;

         return GetIFCClassNameFromExportTable(exporterIFC, categoryId);
      }

      /// <summary>
      /// Get the IFC predefined type assigned in the export layers table for a category.  Cache values to avoid calls to internal code.
      /// </summary>
      /// <param name="exporterIFC">The exporterIFC class.</param>
      /// <param name="element">The element.</param>
      /// <returns>The predefined type.</returns>
      public static string GetIFCTypeFromExportTable(ExporterIFC exporterIFC, Element element)
      {
         Category category = element.Category;
         if (category == null)
            return null;

         ElementId categoryId = category.Id;
         string specialEntry = GetIFCClassNameOrTypeFromSpecialEntry(exporterIFC, element, categoryId, false);
         if (specialEntry != null)
            return specialEntry;

         return GetIFCTypeFromExportTable(exporterIFC, element, categoryId, -1);
      }

      private class ApplicablePsets<T>
      {
         public class PsetsByTypeAndPredefinedType
         {
            public IList<T> ByType { get; set; }
            public IList<T> ByPredefinedType { get; set; }
         }
         public PsetsByTypeAndPredefinedType ByIfcEntity { get; set; } = new PsetsByTypeAndPredefinedType();
         public PsetsByTypeAndPredefinedType ByIfcEntityType { get; set; } = new PsetsByTypeAndPredefinedType();
      }

      /// <summary>
      /// Determines if an IFCEntityType is a non-strict sub-type of another IFCEntityType for the
      /// current IFC schema.
      /// </summary>
      /// <param name="entityType">The child entity type.</param>
      /// <param name="parentType">The parent entity type.</param>
      /// <returns>True if the child is a non-strict sub-type.</returns>
      public static bool IsSubTypeOf(IFCEntityType entityType, IFCEntityType parentType)
      {
         return IfcSchemaEntityTree.IsSubTypeOf(ExporterCacheManager.ExportOptionsCache.FileVersion,
            entityType.ToString(), parentType.ToString(), strict: false);
      }

     /// <summary>
      /// Gets the list of common property sets appropriate to this handle.
      /// </summary>
      /// <param name="prodHnd">The handle.</param>
      /// <param name="psetsToCreate">The list of all property sets.</param>
      /// <returns>The list of property sets for this handle.</returns>
      public static IList<PropertySetDescription> GetCurrPSetsToCreate(IFCAnyHandle prodHnd,
         IList<IList<PropertySetDescription>> psetsToCreate)
      {
         return GetCurrPSetsToCreateGeneric(prodHnd, psetsToCreate, ExporterCacheManager.PropertySetsForTypeCache);
      }

      /// <summary>
      /// Gets the list of predefined property sets appropriate to this handle.
      /// </summary>
      /// <param name="prodHnd">The handle.</param>
      /// <param name="psetsToCreate">The list of all property sets.</param>
      /// <returns>The list of predefined property sets for this handle.</returns>
      public static IList<PreDefinedPropertySetDescription> GetCurrPreDefinedPSetsToCreate(IFCAnyHandle prodHnd,
         IList<IList<PreDefinedPropertySetDescription>> psetsToCreate)
      {
         return GetCurrPSetsToCreateGeneric(prodHnd, psetsToCreate, ExporterCacheManager.PreDefinedPropertySetsForTypeCache);
      }


      public static IFCExportInfoPair GetExportInfoForProperties(IFCAnyHandle prodHnd)
      {
         IFCExportInfoPair exportInfo = null;

         IFCEntityType prodHndType = IFCAnyHandleUtil.GetEntityType(prodHnd);

         // PropertySetEntry will only have an information about IFC entity (or type) for the Pset definition but may not be both
         // Here we will check for both and assign Pset to create equally for both Element or ElementType
         if (IFCAnyHandleUtil.IsSubTypeOf(prodHnd, IFCEntityType.IfcObject))
         {
            ElementId elemId = ExporterCacheManager.HandleToElementCache.Find(prodHnd);
            if (elemId != ElementId.InvalidElementId)
            {
               exportInfo = ExporterCacheManager.ElementToHandleCache.FindPredefinedType(prodHnd, elemId);
            }

            if (exportInfo == null)
            {
               exportInfo = new IFCExportInfoPair(prodHndType);
            }

            // Need to handle backward compatibility for IFC2x3
            if (IFCAnyHandleUtil.IsTypeOf(prodHnd, IFCEntityType.IfcFurnishingElement)
               && (ExporterCacheManager.ExportOptionsCache.ExportAs2x3 || ExporterCacheManager.ExportOptionsCache.ExportAs2x2))
            {
               IFCEntityType altProdHndType = IFCEntityType.UnKnown;
               if (Enum.TryParse<IFCEntityType>("IfcFurnitureType", true, out altProdHndType))
                  exportInfo.SetValue(prodHndType, altProdHndType, exportInfo.ValidatedPredefinedType);
            }
         }
         else if (IFCAnyHandleUtil.IsSubTypeOf(prodHnd, IFCEntityType.IfcTypeObject))
         {
            exportInfo = new IFCExportInfoPair();
            ElementTypeKey etKey = ExporterCacheManager.ElementTypeToHandleCache.Find(prodHnd);
            if (etKey != null)
            {
               exportInfo.SetValueWithPair(etKey.Item2, etKey.Item3);
            }
            else
            {
               exportInfo.SetValueWithPair(prodHndType);
            }

            // Need to handle backward compatibility for IFC2x3
            if (IFCAnyHandleUtil.IsTypeOf(prodHnd, IFCEntityType.IfcFurnitureType)
               && (ExporterCacheManager.ExportOptionsCache.ExportAs2x3 || ExporterCacheManager.ExportOptionsCache.ExportAs2x2))
            {
               IFCEntityType altProdHndType = IFCEntityType.UnKnown;
               if (Enum.TryParse<IFCEntityType>("IfcFurnishingElement", true, out altProdHndType))
                  exportInfo.SetValue(prodHndType, altProdHndType, exportInfo.ValidatedPredefinedType);
            }
         }
         else
         {
            // Default
            exportInfo = new IFCExportInfoPair(prodHndType);
         }

         return exportInfo;
      }

      /// <summary>
      /// Gets the list of common property sets appropriate to this handle.
      /// </summary>
      /// <param name="prodHnd">The handle.</param>
      /// <param name="psetsToCreate">The list of all property sets.</param>
      /// <param name="cacheToUse">The cache for property sets.</param>
      /// <returns>The list of property sets for this handle.</returns>
      public static IList<T> GetCurrPSetsToCreateGeneric<T>(IFCAnyHandle prodHnd,
          IList<IList<T>> psetsToCreate, IDictionary<ExporterCacheManager.PropertySetKey, IList<T>> cacheToUse) where T : Description
      {
         IFCExportInfoPair exportInfo = GetExportInfoForProperties(prodHnd);

         // Find existing Psets list for the given type in the cache
         var cachedPsets = GetCachedPropertySetsGeneric(exportInfo, cacheToUse);
         //Set bool variables to true below to search for property sets If they were not found in cache 
         bool searchPsetsByEntity                     = cachedPsets.ByIfcEntity.ByType == null;
         bool searchPsetsByEntityPredefinedType       = cachedPsets.ByIfcEntity.ByPredefinedType == null;
         bool searchPsetsByEntityType                 = cachedPsets.ByIfcEntityType.ByType == null;
         bool searchPsetsByEntityTypePredefinedType   = cachedPsets.ByIfcEntityType.ByPredefinedType == null;

         IList<T> currPsetsForEntity                     = new List<T>();
         IList<T> currPsetsForEntityPredefinedType       = new List<T>();
         IList<T> currPsetsForEntityType                 = new List<T>();
         IList<T> currPsetsForEntityTypePredefinedType   = new List<T>();
         if (searchPsetsByEntity || searchPsetsByEntityPredefinedType || searchPsetsByEntityType || searchPsetsByEntityTypePredefinedType)
         {
            foreach (IList<T> currStandard in psetsToCreate)
            {
               var applicablePsets = GetApplicablePropertySets(exportInfo, currStandard);
               if (searchPsetsByEntity)
                  Union(ref currPsetsForEntity, applicablePsets.ByIfcEntity.ByType);
               if (searchPsetsByEntityPredefinedType)
                  Union(ref currPsetsForEntityPredefinedType, applicablePsets.ByIfcEntity.ByPredefinedType);
               if (searchPsetsByEntityType)
                  Union(ref currPsetsForEntityType, applicablePsets.ByIfcEntityType.ByType);
               if (searchPsetsByEntityTypePredefinedType)
                  Union(ref currPsetsForEntityTypePredefinedType, applicablePsets.ByIfcEntityType.ByPredefinedType);
            }

            if (searchPsetsByEntity)
               cacheToUse[new ExporterCacheManager.PropertySetKey(exportInfo.ExportInstance, null)] = currPsetsForEntity;

            if (searchPsetsByEntityPredefinedType)
               cacheToUse[new ExporterCacheManager.PropertySetKey(exportInfo.ExportInstance, exportInfo.ValidatedPredefinedType)] = currPsetsForEntityPredefinedType;

            if (searchPsetsByEntityType)
               cacheToUse[new ExporterCacheManager.PropertySetKey(exportInfo.ExportType, null)] = currPsetsForEntityType;

            if (searchPsetsByEntityTypePredefinedType)
               cacheToUse[new ExporterCacheManager.PropertySetKey(exportInfo.ExportType, exportInfo.ValidatedPredefinedType)] = currPsetsForEntityTypePredefinedType;
         }

         if (!searchPsetsByEntity)
            currPsetsForEntity = cachedPsets.ByIfcEntity.ByType;

         if (!searchPsetsByEntityPredefinedType)
            currPsetsForEntityPredefinedType = cachedPsets.ByIfcEntity.ByPredefinedType;

         if (!searchPsetsByEntityType)
            currPsetsForEntityType = cachedPsets.ByIfcEntityType.ByType;

         if (!searchPsetsByEntityTypePredefinedType)
            currPsetsForEntityTypePredefinedType = cachedPsets.ByIfcEntityType.ByPredefinedType;

         var currPsets = currPsetsForEntity.ToList();//make independent copy. Without this we will have a bug.
         currPsets.AddRange(currPsetsForEntityPredefinedType);
         currPsets.AddRange(currPsetsForEntityType);
         currPsets.AddRange(currPsetsForEntityTypePredefinedType);
         return currPsets;
      }

      /// <summary>
      /// Get applicable PropertySets for an entity type with optionaly condition for PredefinedType
      ///    The logic needs some explanation here. The quality of IFC documentation is rather poor especially the earlier version (i.e. IFC2x2, IFC2x3)
      ///    The use of ObjectType in the PSD is unclear sometime it is a duplicate of applicable classes, sometime it is showing PredefinedType (in IFC2x2),
      ///    sometime purely useless information. Due to that, we will also check ObjectType for applicable entity if not present, and also cheked for
      ///    PredefinedType if not present
      /// </summary>
      /// <param name="exportInfo">the export infor pair</param>
      /// <param name="psetList">the pset list to iterate</param>
      /// <returns>filtered results of the applicable Psets. Output psets are grouped by type they relate to.</returns>
      static ApplicablePsets<T> GetApplicablePropertySets<T>(IFCExportInfoPair exportInfo, IEnumerable<T> psetList) where T : Description
      {
         IList<T> applicablePsetsByType = null;
         IList<T> applicablePsetsByPredefinedType = null;
         ApplicablePsets<T> applicablePsets = new ApplicablePsets<T>();
         applicablePsets.ByIfcEntity.ByType = new List<T>();
         applicablePsets.ByIfcEntity.ByPredefinedType = new List<T>();
         applicablePsets.ByIfcEntityType.ByType = new List<T>();
         applicablePsets.ByIfcEntityType.ByPredefinedType = new List<T>();
         foreach (T currDesc in psetList)
         {
            bool toAdd = false;
            if (currDesc.IsAppropriateEntityType(exportInfo.ExportInstance) || currDesc.IsAppropriateObjectType(exportInfo.ExportInstance))
            {
               toAdd = true;
               applicablePsetsByType = applicablePsets.ByIfcEntity.ByType;
               applicablePsetsByPredefinedType = applicablePsets.ByIfcEntity.ByPredefinedType;
            }
            // ObjectType if the Applicable type is missing
            else if (currDesc.IsAppropriateEntityType(exportInfo.ExportType) || currDesc.IsAppropriateObjectType(exportInfo.ExportType))
            {
               toAdd = true;
               applicablePsetsByType = applicablePsets.ByIfcEntityType.ByType;
               applicablePsetsByPredefinedType = applicablePsets.ByIfcEntityType.ByPredefinedType;
            }

            if (toAdd)
            {
               if (string.IsNullOrEmpty(currDesc.PredefinedType))
               {
                  applicablePsetsByType.Add(currDesc);
               }
               else if (!string.IsNullOrEmpty(currDesc.PredefinedType) && currDesc.PredefinedType.Equals(exportInfo.ValidatedPredefinedType, StringComparison.InvariantCultureIgnoreCase))
               {
                  applicablePsetsByPredefinedType.Add(currDesc);
               }
               // Also check ObjectType since the predefinedType seems to go here for the earlier versions of IFC
               else if (!string.IsNullOrEmpty(currDesc.ObjectType) && currDesc.ObjectType.Equals(exportInfo.ValidatedPredefinedType, StringComparison.InvariantCultureIgnoreCase))
               {
                  applicablePsetsByType.Add(currDesc);
               }
            }
         }
         return applicablePsets;
      }

      /// <summary>
      /// Get PropertySets from cache.
      /// Current logic searches psets by 4 different PropertySet keys:
      ///   1)IfcEntity,
      ///   2)IfcEntity + PredefinedType,
      ///   3)IfcEntityType,
      ///   4)IfcEntityType + PredefinedType.
      /// Found psets are stored separately in ApplicablePsets object.
      /// </summary>
      /// <param name="exportInfo">the export infor pair</param>
      /// <returns>ApplicablePsets object with 4 containers which store 4 different groups of psets.
      /// If size of container is 0 then this means that search hasn't found any Psets associated with this type 
      /// which is why empty container was cached. This function finds it and returns.
      /// If container is null then this means that info for this type is not cached because search has never been performed for it.
      /// </returns>
      private static ApplicablePsets<T> GetCachedPropertySetsGeneric<T>(IFCExportInfoPair exportInfo, IDictionary<ExporterCacheManager.PropertySetKey, IList<T>> cacheToUse) where T : Description
      {
         ApplicablePsets<T> applicablePsets = new ApplicablePsets<T>();
         IList<T> tmpCachedPsets = null;

         if (cacheToUse.TryGetValue(new ExporterCacheManager.PropertySetKey(exportInfo.ExportInstance, null), out tmpCachedPsets))
         {
            applicablePsets.ByIfcEntity.ByType = tmpCachedPsets;
         }
         if (cacheToUse.TryGetValue(new ExporterCacheManager.PropertySetKey(exportInfo.ExportType, null), out tmpCachedPsets))
         {
            applicablePsets.ByIfcEntityType.ByType = tmpCachedPsets;
         }

         if (string.IsNullOrEmpty(exportInfo.ValidatedPredefinedType))
         {
            applicablePsets.ByIfcEntity.ByPredefinedType = new List<T>();
            applicablePsets.ByIfcEntityType.ByPredefinedType = new List<T>();
         }
         else
         {
            if (cacheToUse.TryGetValue(new ExporterCacheManager.PropertySetKey(exportInfo.ExportInstance, exportInfo.ValidatedPredefinedType), out tmpCachedPsets))
            {
               applicablePsets.ByIfcEntity.ByPredefinedType = tmpCachedPsets;
            }
            if (cacheToUse.TryGetValue(new ExporterCacheManager.PropertySetKey(exportInfo.ExportType, exportInfo.ValidatedPredefinedType), out tmpCachedPsets))
            {
               applicablePsets.ByIfcEntityType.ByPredefinedType = tmpCachedPsets;
            }
         }

         return applicablePsets;
      }

      /// <summary>
      /// Exports Pset_Draughting for IFC 2x2 standard.
      /// </summary>
      /// <param name="exporterIFC">The IFC exporter object.</param>
      /// <param name="element ">The element whose properties are exported.</param>
      /// <param name="productWrapper">The ProductWrapper object.</param>
      private static void ExportPsetDraughtingFor2x2(ExporterIFC exporterIFC, Element element, ProductWrapper productWrapper)
      {
         IFCFile file = exporterIFC.GetFile();
         using (IFCTransaction transaction = new IFCTransaction(file))
         {
            IFCAnyHandle ownerHistory = ExporterCacheManager.OwnerHistoryHandle;

            string catName = CategoryUtil.GetCategoryName(element);
            Color color = CategoryUtil.GetElementColor(element);
            HashSet<IFCAnyHandle> nameAndColorProps = new HashSet<IFCAnyHandle>();

            nameAndColorProps.Add(PropertyUtil.CreateLabelPropertyFromCache(file, null, "Layername", catName, PropertyValueType.SingleValue, true, null));

            //color
            {
               HashSet<IFCAnyHandle> colorProps = new HashSet<IFCAnyHandle>();
               colorProps.Add(PropertyUtil.CreateIntegerPropertyFromCache(file, "Red", color.Red, PropertyValueType.SingleValue));
               colorProps.Add(PropertyUtil.CreateIntegerPropertyFromCache(file, "Green", color.Green, PropertyValueType.SingleValue));
               colorProps.Add(PropertyUtil.CreateIntegerPropertyFromCache(file, "Blue", color.Blue, PropertyValueType.SingleValue));

               string propertyName = "Color";
               nameAndColorProps.Add(IFCInstanceExporter.CreateComplexProperty(file, propertyName, null, propertyName, colorProps));
            }

            HashSet<IFCAnyHandle> relatedObjects = new HashSet<IFCAnyHandle>(productWrapper.GetAllObjects());
            if (!ExporterCacheManager.CreatedSpecialPropertySets.TryAppend(element.Id, relatedObjects))
            {
               string name = "Pset_Draughting";   // IFC 2x2 standard
               string psetGuid = GUIDUtil.GenerateIFCGuidFrom(
                  GUIDUtil.CreateGUIDString(element, name));
               IFCAnyHandle propertySetDraughting = IFCInstanceExporter.CreatePropertySet(file, psetGuid, ownerHistory, name, null, nameAndColorProps);
               HashSet<IFCAnyHandle> propertySets = new HashSet<IFCAnyHandle>() { propertySetDraughting };
               ExporterCacheManager.CreatedSpecialPropertySets.Add(element.Id, propertySets, relatedObjects);
            }
            
            transaction.Commit();
         }
      }

      /// <summary>
      /// Exports the element properties.
      /// </summary>
      /// <param name="exporterIFC">The IFC exporter object.</param>
      /// <param name="element">The element whose properties are exported.</param>
      /// <param name="productWrapper">The ProductWrapper object.</param>
      private static void ExportElementProperties(ExporterIFC exporterIFC, Element element, ProductWrapper productWrapper)
      {
         if (productWrapper.IsEmpty())
            return;

         IFCFile file = exporterIFC.GetFile();
         using (IFCTransaction transaction = new IFCTransaction(file))
         {
            Document doc = element.Document;

            ElementType elemType = doc.GetElement(element.GetTypeId()) as ElementType;

            IFCAnyHandle ownerHistory = ExporterCacheManager.OwnerHistoryHandle;

            ICollection<IFCAnyHandle> productSet = productWrapper.GetAllObjects();
            IList<IList<PropertySetDescription>> psetsToCreate = ExporterCacheManager.ParameterCache.PropertySets;

            // In some cases, like multi-story stairs and ramps, we may have the same Pset used for multiple levels.
            // If ifcParams is null, re-use the property set.
            IDictionary<Tuple<Element, Element, string>, IFCAnyHandle> createdPropertySets =
                new Dictionary<Tuple<Element, Element, string>, IFCAnyHandle>();
            IDictionary<IFCAnyHandle, HashSet<IFCAnyHandle>> relDefinesByPropertiesMap =
                new Dictionary<IFCAnyHandle, HashSet<IFCAnyHandle>>();

            foreach (IFCAnyHandle prodHnd in productSet)
            {
               // Need to check whether the handle is valid. In some cases object that has parts may not be complete and may have orphaned handles that are not valid
               if (IFCAnyHandleUtil.IsNullOrHasNoValue(prodHnd))
                  continue;

               IList<PropertySetDescription> currPsetsToCreate = GetCurrPSetsToCreate(prodHnd, psetsToCreate);
               if (currPsetsToCreate.Count == 0)
                  continue;

               ElementId overrideElementId = ExporterCacheManager.HandleToElementCache.Find(prodHnd);
               Element elementToUse = (overrideElementId == ElementId.InvalidElementId) ? element : doc.GetElement(overrideElementId);
               ElementType elemTypeToUse = (overrideElementId == ElementId.InvalidElementId) ? elemType : doc.GetElement(elementToUse.GetTypeId()) as ElementType;
               if (elemTypeToUse == null)
                  elemTypeToUse = elemType;

               IFCExportBodyParams ifcParams = productWrapper.FindExtrusionCreationParameters(prodHnd);

               foreach (PropertySetDescription currDesc in currPsetsToCreate)
               {
                  // Last conditional check: if the property set comes from a ViewSchedule, check if the element is in the schedule.
                  if ((currDesc.ViewScheduleId != ElementId.InvalidElementId) &&
                     (!ExporterCacheManager.ViewScheduleElementCache[currDesc.ViewScheduleId].Contains(elementToUse.Id)))
                     continue;

                  Tuple<Element, Element, string> propertySetKey = new Tuple<Element, Element, string>(elementToUse, elemTypeToUse, currDesc.Name);
                  IFCAnyHandle propertySet = null;
                  if ((ifcParams != null) || (!createdPropertySets.TryGetValue(propertySetKey, out propertySet)))
                  {
                     ElementOrConnector elementOrConnector = new ElementOrConnector(elementToUse);
                     ISet<IFCAnyHandle> props = currDesc.ProcessEntries(file, exporterIFC, ifcParams, elementOrConnector, elemTypeToUse, prodHnd);
                     if (props.Count > 0)
                     {
                        string paramSetName = currDesc.Name;
                        string guid = GUIDUtil.GenerateIFCGuidFrom(
                           GUIDUtil.CreateGUIDString(IFCEntityType.IfcPropertySet, paramSetName, prodHnd));

                        propertySet = IFCInstanceExporter.CreatePropertySet(file, guid, ownerHistory, paramSetName, currDesc.DescriptionOfSet, props);
                        if (ifcParams == null)
                           createdPropertySets[propertySetKey] = propertySet;
                     }
                  }

                  if (propertySet != null)
                  {
                     IFCAnyHandle prodHndToUse = prodHnd;
                     DescriptionCalculator ifcRDC = currDesc.DescriptionCalculator;
                     if (ifcRDC != null)
                     {
                        IFCAnyHandle overrideHnd = ifcRDC.RedirectDescription(exporterIFC, elementToUse);
                        if (!IFCAnyHandleUtil.IsNullOrHasNoValue(overrideHnd))
                           prodHndToUse = overrideHnd;
                     }

                     HashSet<IFCAnyHandle> relatedObjects = null;
                     if (!relDefinesByPropertiesMap.TryGetValue(propertySet, out relatedObjects))
                     {
                        relatedObjects = new HashSet<IFCAnyHandle>();
                        relDefinesByPropertiesMap[propertySet] = relatedObjects;
                     }
                     relatedObjects.Add(prodHndToUse);
                  }
               }
            }

            foreach (KeyValuePair<IFCAnyHandle, HashSet<IFCAnyHandle>> relDefinesByProperties in relDefinesByPropertiesMap)
            {
               CreateRelDefinesByProperties(file, ownerHistory, null, null, relDefinesByProperties.Value, relDefinesByProperties.Key);
            }

            transaction.Commit();
         }

         if (ExporterCacheManager.ExportOptionsCache.ExportAs2x2)
            ExportPsetDraughtingFor2x2(exporterIFC, element, productWrapper);
      }

      /// <summary>
      /// Exports the IFC element quantities.
      /// </summary>
      /// <param name="exporterIFC">The IFC exporter object.</param>
      /// <param name="element ">The element whose quantities are exported.</param>
      /// <param name="productWrapper">The ProductWrapper object.</param>
      private static void ExportElementQuantities(ExporterIFC exporterIFC, Element element, ProductWrapper productWrapper)
      {
         if (productWrapper.IsEmpty())
            return;

         IList<IList<QuantityDescription>> quantitiesToCreate = ExporterCacheManager.ParameterCache.Quantities;
         if (quantitiesToCreate.Count == 0)
            return;

         IFCFile file = exporterIFC.GetFile();
         using (IFCTransaction transaction = new IFCTransaction(file))
         {
            Document doc = element.Document;

            ElementType elemType = doc.GetElement(element.GetTypeId()) as ElementType;

            IFCAnyHandle ownerHistory = ExporterCacheManager.OwnerHistoryHandle;

            ICollection<IFCAnyHandle> productSet = productWrapper.GetAllObjects();

            foreach (IList<QuantityDescription> currStandard in quantitiesToCreate)
            {
               foreach (QuantityDescription currDesc in currStandard)
               {
                  foreach (IFCAnyHandle prodHnd in productSet)
                  {
                     // For an aggregate, the member product must be processed with its element and type
                     ElementId overrideElementId = ExporterCacheManager.HandleToElementCache.Find(prodHnd);
                     Element elementToUse = (overrideElementId == ElementId.InvalidElementId) ? element : doc.GetElement(overrideElementId);
                     Element elemOfProd = doc.GetElement(ExporterCacheManager.HandleToElementCache.Find(prodHnd));
                     if (elemOfProd != null)
                        elementToUse = elemOfProd;
                     ElementType elemTypeToUse = (overrideElementId == ElementId.InvalidElementId) ? elemType : doc.GetElement(elementToUse.GetTypeId()) as ElementType;
                     if (elemTypeToUse == null)
                        elemTypeToUse = elemType;

                     if (currDesc.IsAppropriateType(prodHnd) && !ExporterCacheManager.QtoSetCreated.Contains((prodHnd, currDesc.Name)))
                     {
                        HashSet<string> uniqueQuantityNames = new HashSet<string>();
                        HashSet<IFCAnyHandle> quantities = new HashSet<IFCAnyHandle>();

                        HashSet<IFCAnyHandle> addQuantity;
                        if (ExporterCacheManager.ComplexPropertyCache.TryGetValue(prodHnd, out addQuantity))
                        {
                           foreach (IFCAnyHandle addQty in addQuantity)
                           {
                              quantities.Add(addQty);
                              string addQtyName = IFCAnyHandleUtil.GetStringAttribute(addQty, "Name");
                              uniqueQuantityNames.Add(addQtyName);
                           }
                        }

                        IFCExportBodyParams ifcParams = productWrapper.FindExtrusionCreationParameters(prodHnd);

                        HashSet<IFCAnyHandle> qtyFromInit = currDesc.ProcessEntries(file, exporterIFC, ifcParams, elementToUse, elemTypeToUse);
                        foreach (IFCAnyHandle qty in qtyFromInit)
                        {
                           if (IFCAnyHandleUtil.IsNullOrHasNoValue(qty) || IFCAnyHandleUtil.IsNullOrHasNoValue(qty))
                              continue;

                           string qtyName = IFCAnyHandleUtil.GetStringAttribute(qty, "Name");
                           // Check for duplicate name. Do not write a quantity that is already defined
                           if (!uniqueQuantityNames.Contains(qtyName))
                           {
                              quantities.Add(qty);
                              uniqueQuantityNames.Add(qtyName);
                           }
                        }

                        if (quantities.Count > 0)
                        {
                           string paramSetName = currDesc.Name;
                           string methodName = currDesc.MethodOfMeasurement;
                           string description = currDesc.DescriptionOfSet;


                           // Skip if the elementHandle has the associated QuantitySet has been created before
                           if (!ExporterCacheManager.QtoSetCreated.Contains((prodHnd, paramSetName)))
                           {
                              string guid = GUIDUtil.GenerateIFCGuidFrom(
                                 GUIDUtil.CreateGUIDString(IFCEntityType.IfcElementQuantity, 
                                 "QuantitySet: " + paramSetName, prodHnd));
                              IFCAnyHandle quantity = IFCInstanceExporter.CreateElementQuantity(file, 
                                 prodHnd, guid, ownerHistory, paramSetName, description, 
                                 methodName, quantities);
                              IFCAnyHandle prodHndToUse = prodHnd;
                              DescriptionCalculator ifcRDC = currDesc.DescriptionCalculator;
                              if (ifcRDC != null)
                              {
                                 IFCAnyHandle overrideHnd = ifcRDC.RedirectDescription(exporterIFC, element);
                                 if (!IFCAnyHandleUtil.IsNullOrHasNoValue(overrideHnd))
                                    prodHndToUse = overrideHnd;
                              }
                              HashSet<IFCAnyHandle> relatedObjects = new HashSet<IFCAnyHandle>();
                              relatedObjects.Add(prodHndToUse);
                              CreateRelDefinesByProperties(file, ownerHistory, null, null, relatedObjects, quantity);
                           }
                        }
                     }
                  }
               }
            }
            transaction.Commit();
         }
      }

      /// <summary>Exports the element classification(s)./// </summary>
      /// <param name="exporterIFC">The IFC exporter object.</param>
      /// <param name="element">The element whose classifications are exported.</param>
      /// <param name="productWrapper">The ProductWrapper object.</param>
      private static void ExportElementUniformatClassifications(ExporterIFC exporterIFC,
         Element element, ProductWrapper productWrapper)
      {
         if (productWrapper.IsEmpty())
            return;

         IFCFile file = exporterIFC.GetFile();
         using (IFCTransaction transaction = new IFCTransaction(file))
         {
            ICollection<IFCAnyHandle> productSet = productWrapper.GetAllObjects();
            ClassificationUtil.CreateUniformatClassification(exporterIFC, file, element, productSet.ToList(), IFCEntityType.IfcElement);
            transaction.Commit();
         }
      }

      private static void ExportElementClassifications(ExporterIFC exporterIFC, Element element, ProductWrapper productWrapper)
      {
         if (productWrapper.IsEmpty())
            return;

         IFCFile file = exporterIFC.GetFile();
         using (IFCTransaction transaction = new IFCTransaction(file))
         {
            ICollection<IFCAnyHandle> productSet = productWrapper.GetAllObjects();
            foreach (IFCAnyHandle prodHnd in productSet)
            {
               if (productSet.Count > 1 && prodHnd == productSet.First() && IFCAnyHandleUtil.IsTypeOf(prodHnd, IFCEntityType.IfcElementAssembly))
                  continue;   //Classification for the ELementAssembly should have been created before when processing ElementAssembly

               // No need to check the subtype since Classification can be assigned to IfcRoot
               ClassificationUtil.CreateClassification(exporterIFC, file, element, prodHnd);
            }
            transaction.Commit();
         }
      }

      /// <summary>
      /// Export IFC common property set, Quantity (if set) and Classification (or Uniformat for COBIE) information for an element.
      /// </summary>
      /// <param name="exporterIFC">The exporterIFC class.</param>
      /// <param name="element">The element.</param>
      /// <param name="productWrapper">The ProductWrapper class that contains the associated IFC handles.</param>
      public static void ExportRelatedProperties(ExporterIFC exporterIFC, Element element, ProductWrapper productWrapper)
      {
         ExportElementProperties(exporterIFC, element, productWrapper);
         if (ExporterCacheManager.ExportOptionsCache.ExportBaseQuantities && !(ExporterCacheManager.ExportOptionsCache.ExportAsCOBIE))
            ExportElementQuantities(exporterIFC, element, productWrapper);
         ExportElementClassifications(exporterIFC, element, productWrapper);                     // Exporting ClassificationCode from IFC parameter 
         ExportElementUniformatClassifications(exporterIFC, element, productWrapper);            // Default classification, if filled out.
      }

      /// <summary>
      /// Checks an enumTypeValue to determine if it is defined or not.
      /// </summary>
      /// <param name="enumTypeValue">The enum type value to check.</param>
      /// <returns>True if the enumTypeValue is null, empty, or set to "NOTDEFINED".</returns>
      public static bool IsNotDefined(string enumTypeValue)
      {
         return (string.IsNullOrWhiteSpace(enumTypeValue) || (string.Compare(enumTypeValue, "NOTDEFINED", true) == 0));
      }

      /// <summary>
      /// Get the string value from IFC_EXPORT_PREDEFINEDTYPE* built-in parameters.
      /// </summary>
      /// <param name="element">The element.</param>
      /// <param name="elementType">The optional element type.</param>
      /// <returns>The string value of IFC_EXPORT_PREDEFINEDTYPE if assigned, or 
      /// IFC_EXPORT_PREDEFINEDTYPE_TYPE if not.</returns>
      public static string GetExportTypeFromTypeParameter(Element element, Element elementType)
      {
         BuiltInParameter paramId = (element is ElementType) ? BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE :
            BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE;
         Parameter exportElementParameter = element.get_Parameter(paramId);
         string pdefFromParam = exportElementParameter?.AsString();
         if (string.IsNullOrEmpty(pdefFromParam) && (paramId == BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE))
         {
            if (elementType == null)
               elementType = element.Document.GetElement(element.GetTypeId());
            exportElementParameter = elementType?.get_Parameter(BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE);
            pdefFromParam = exportElementParameter?.AsString();
         }

         return pdefFromParam;
      }

      /// <summary>
      /// Gets the export entity and predefined type information as reported by the
      /// IFC_EXPORT_ELEMENT*_AS parameter.
      /// </summary>
      /// <param name="element">The element.</param>
      /// <param name="restrictedGroup">The subset of IFC entities allowed.</param>
      /// <returns>The IFC entity/predefined type pair.</returns>
      public static IFCExportInfoPair GetIFCExportElementParameterInfo(Element element, 
         IFCEntityType restrictedGroup)
      {
         if (element == null)
            return IFCExportInfoPair.UnKnown;

         BuiltInParameter paramId = (element is ElementType) ? BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS :
            BuiltInParameter.IFC_EXPORT_ELEMENT_AS;
         Parameter exportElementParameter = element.get_Parameter(paramId);
         string symbolClassName = exportElementParameter?.AsString();
         if (string.IsNullOrEmpty(symbolClassName) && (paramId == BuiltInParameter.IFC_EXPORT_ELEMENT_AS))
         {
            Element elementType = element.Document.GetElement(element.GetTypeId());
            exportElementParameter = elementType?.get_Parameter(BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS);
            symbolClassName = exportElementParameter?.AsString();
         }

         IFCExportInfoPair exportType = IFCExportInfoPair.UnKnown;

         string predefType = null;
         if (!string.IsNullOrEmpty(symbolClassName))
         {
            ExportEntityAndPredefinedType(symbolClassName, out symbolClassName, out predefType);

            // Ignore the value if we can't process it.
            IFCExportInfoPair overrideExportType = ElementFilteringUtil.GetExportTypeFromClassName(symbolClassName);
            if (!overrideExportType.IsUnKnown && 
               IfcSchemaEntityTree.IsSubTypeOf(ExporterCacheManager.ExportOptionsCache.FileVersion, overrideExportType.ExportInstance, restrictedGroup))
            { 
               exportType = overrideExportType;
            }
         }

         if (!string.IsNullOrEmpty(predefType))
         {
            exportType.ValidatedPredefinedType = predefType;
         }

         return exportType;
      }

      private static IFCExportInfoPair GetExportTypeForFurniture(ExporterIFC exporterIFC, Element element)
      {
         if (element.GroupId == null || element.GroupId == ElementId.InvalidElementId)
            return IFCExportInfoPair.UnKnown;

         IFCExportInfoPair groupType;
         if (ExporterCacheManager.GroupCache.TryGetValue(element.GroupId, out GroupInfo groupInfo) && groupInfo.GroupType.ExportInstance != IFCEntityType.UnKnown)
         {
            groupType = groupInfo.GroupType;
         }
         else
         {
            Element groupElement = element.Document.GetElement(element.GroupId);
            IFCExportInfoPair exportGroupAs = GetObjectExportType(exporterIFC, groupElement, out _);
            ExporterCacheManager.GroupCache.RegisterOrUpdateGroupType(element.GroupId, exportGroupAs);
            groupType = exportGroupAs;
         }

         if (groupType.ExportInstance == IFCEntityType.IfcFurniture)
         {
            return new IFCExportInfoPair(IFCEntityType.IfcSystemFurnitureElement, "NOTDEFINED");
         }

         return IFCExportInfoPair.UnKnown;
      }

      private static IFCExportInfoPair OverrideExportTypeForStructuralFamilies(Element element,
         IFCExportInfoPair originalExportInfoPair)
      {
         if ((!originalExportInfoPair.IsUnKnown) && 
            (originalExportInfoPair.ExportInstance != IFCEntityType.IfcBuildingElementProxy) && 
            (originalExportInfoPair.ExportType != IFCEntityType.IfcBuildingElementProxyType))
            return originalExportInfoPair;

         FamilyInstance familyInstance = element as FamilyInstance;
         if (familyInstance == null)
            return originalExportInfoPair;

         string enumTypeValue = originalExportInfoPair.ValidatedPredefinedType;

         switch (familyInstance.StructuralType)
         {
            case Autodesk.Revit.DB.Structure.StructuralType.Beam:
               if (string.IsNullOrEmpty(enumTypeValue))
                  enumTypeValue = "BEAM";
               return new IFCExportInfoPair(IFCEntityType.IfcBeam, enumTypeValue);
            case Autodesk.Revit.DB.Structure.StructuralType.Brace:
               if (string.IsNullOrEmpty(enumTypeValue))
                  enumTypeValue = "BRACE";
               return new IFCExportInfoPair(IFCEntityType.IfcMember, enumTypeValue);
            case Autodesk.Revit.DB.Structure.StructuralType.Footing:
               return new IFCExportInfoPair(IFCEntityType.IfcFooting, enumTypeValue);
            case Autodesk.Revit.DB.Structure.StructuralType.Column:
               if (string.IsNullOrEmpty(enumTypeValue))
                  enumTypeValue = "COLUMN";
               return new IFCExportInfoPair(IFCEntityType.IfcColumn, enumTypeValue);
         }

         return originalExportInfoPair;
      }

      /// <summary>
      /// Gets export type for an element in pair information of the IfcEntity and its type.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="element">The element.</param>
      /// <param name="restrictedGroup">The base class of the allowed entity instances.</param>
      /// <param name="enumTypeValue">The output string value represents the enum type.</param>
      /// <returns>The IFCExportInfoPair.</returns>
      /// <remarks>If restrictedGroup is null, GetExportType will return any legal IFC entity.
      /// However, most uses of this function should have some restriction.</remarks>
      private static IFCExportInfoPair GetExportType(ExporterIFC exporterIFC, Element element,
         IFCEntityType restrictedGroup, out string enumTypeValue)
      {
         enumTypeValue = null;

         // Overall outline of this function.  We do the checks in order so that if an earlier
         // check finds an acceptable value, we won't do the later ones.
         // 1. Check specifically to see if the user has marked the category as "Not exported"
         // in the IFC Export Options table.  If so, this overrides all other settings.
         // 2. For the special case of an element in a group, check if it in an IfcFurniture group.
         // 3. Check the parameters IFC_EXPORT_ELEMENT*_AS.
         // 4. Look at class specified by the IFC Export Options table in step 1, if set.
         // 5. Check at a pre-defined mapping from Revit category to IFC entity and pre-defined type.
         // 6. Check whether the intended Entity type is inside the export exclusion set.
         // 7. Check whether we override IfcBuildingElementProxy/Unknown values with structural known values.
         // 8. Check to see if we should override the ValidatedPredefinedType from IFC_EXPORT_PREDEFINEDTYPE*.

         // Steps start below.

         // 1. Check specifically to see if the user has marked the category as "Not exported"
         // in the IFC Export Options table.  If so, this overrides all other settings.
         // This will also return the ifcClassName, which will be checked later (if it isn't
         // set to "Not exported".
         // Note that this means that if the Walls category is not exported, but a wall is set to be
         // exported as, e.g., an IfcCeilingType, it won't be exported.  We may want to reconsider this
         // in the future based on customer feedback.
         ElementId categoryId;
         string ifcClassName = GetIFCClassNameFromExportTable(exporterIFC, element, out categoryId);
         if (categoryId == ElementId.InvalidElementId)
            return IFCExportInfoPair.UnKnown;

         // 2. If Element is contained within a Group that is exported as IfcFurniture, it should be 
         // exported as an IfcSystemFurnitureElement, regardless of other settings.
         IFCExportInfoPair exportType = GetExportTypeForFurniture(exporterIFC, element);

         // 3. Check the parameters IFC_EXPORT_ELEMENT*_AS.
         if (exportType.IsUnKnown)
         {
            exportType = GetIFCExportElementParameterInfo(element, restrictedGroup);
         }

         // 4. Look at class specified by the IFC Export Options table in step 1.
         if (exportType.IsUnKnown && !string.IsNullOrEmpty(ifcClassName))
         {
            if (string.IsNullOrEmpty(enumTypeValue))
               enumTypeValue = GetIFCTypeFromExportTable(exporterIFC, element);
            // if using name, override category id if match is found.
            if (!ifcClassName.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
               exportType = ElementFilteringUtil.GetExportTypeFromClassName(ifcClassName);
               exportType.ValidatedPredefinedType = enumTypeValue;
            }
         }
         
         // 5. Check at a pre-defined mapping from Revit category to IFC entity and pre-defined type.
         if (exportType.IsUnKnown)
         {
            exportType = ElementFilteringUtil.GetExportTypeFromCategoryId(categoryId);
            if (string.IsNullOrEmpty(enumTypeValue))
               enumTypeValue = exportType.ValidatedPredefinedType;
         }

         // 6. Check whether the intended Entity type is inside the export exclusion set.  If it is,
         // we are done - we won't export it.
         if (ExporterCacheManager.ExportOptionsCache.IsElementInExcludeList(exportType.ExportInstance))
            return IFCExportInfoPair.UnKnown;

         // 7. Check whether we override IfcBuildingElementProxy/Unknown values with 
         // structural known values.
         exportType = OverrideExportTypeForStructuralFamilies(element, exportType);

         // 8. Check to see if we should override the ValidatedPredefinedType from
         // IFC_EXPORT_PREDEFINEDTYPE*.
         string pdefFromParam = GetExportTypeFromTypeParameter(element, null);
         if (!string.IsNullOrEmpty(pdefFromParam))
            enumTypeValue = pdefFromParam;
         
         if (!string.IsNullOrEmpty(enumTypeValue))
            exportType.ValidatedPredefinedType = enumTypeValue;

         // Set the out parameter here.
         enumTypeValue = exportType.ValidatedPredefinedType;

         if (string.IsNullOrEmpty(enumTypeValue))
            enumTypeValue = "NOTDEFINED";

         return exportType;
      }

      /// <summary>
      /// Gets export type for an element in pair information of the IfcEntity and its type.
      /// Restricted to sub-types of IfcProduct.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="element">The element.</param>
      /// <param name="enumTypeValue">The output string value represents the enum type.</param>
      /// <returns>The IFCExportInfoPair.</returns>
      public static IFCExportInfoPair GetProductExportType(ExporterIFC exporterIFC, Element element,
        out string enumTypeValue)
      {
         return GetExportType(exporterIFC, element, IFCEntityType.IfcProduct, out enumTypeValue);
      }

      /// <summary>
      /// Gets export type for an element in pair information of the IfcEntity and its type.
      /// Restricted to sub-types of IfcObject.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="element">The element.</param>
      /// <param name="enumTypeValue">The output string value represents the enum type.</param>
      /// <returns>The IFCExportInfoPair.</returns>
      public static IFCExportInfoPair GetObjectExportType(ExporterIFC exporterIFC, Element element,
        out string enumTypeValue)
      {
         return GetExportType(exporterIFC, element, IFCEntityType.IfcObject, out enumTypeValue);
      }

      /// <summary>
      /// Get export entity and predefinedType from symbolClassName. Generally it should come from
      /// the built-in parameters (for symbolClassName)
      /// </summary>
      /// <param name="symbolClassName">the IFC_EXPORT_ELEMENT_AS parameter value</param>
      /// <param name="exportEntity">output export entity string</param>
      /// <param name="predefinedTypeStr">output predefinedType string</param>
      public static void ExportEntityAndPredefinedType(string symbolClassName, out string exportEntity, out string predefinedTypeStr)
      {
         exportEntity = symbolClassName;
         predefinedTypeStr = string.Empty;

         if (!string.IsNullOrEmpty(symbolClassName))
         {
            // We are expanding the format to also support: <IfcTypeEntity>.<predefinedType>.
            // Therefore we need to parse here. This format will override value in IFCExportType
            // if any.
            string[] splitResult = symbolClassName.Split(new Char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (splitResult.Length > 1)
            {
               // found <IfcTypeEntity>.<PredefinedType>
               exportEntity = splitResult[0].Trim();
               predefinedTypeStr = splitResult[1].Trim();
            }
         }
      }

      /// <summary>
      /// Create IFC Entity Type in a generic way from an Element
      /// </summary>
      /// <param name="element">the Element</param>
      /// <param name="exportType">the export Type</param>
      /// <param name="file">the IFC File</param>
      /// <param name="ownerHistory">the OwnerHistory</param>
      /// <param name="predefinedType">PredefinedType</param>
      /// <returns>IFCAnyHandle if successful, null otherwise</returns>
      public static IFCAnyHandle CreateGenericTypeFromElement(Element element, 
         IFCExportInfoPair exportType, IFCFile file, IFCAnyHandle ownerHistory, 
         string predefinedType, ProductWrapper productWrapper)
      {
         Document doc = element.Document;
         ElementId typeElemId = element.GetTypeId();
         ElementType elementType = doc.GetElement(typeElemId) as ElementType;

         IFCAnyHandle entType = null;

         if (elementType != null)
         {
            entType = ExporterCacheManager.ElementTypeToHandleCache.Find(elementType, exportType);
            if (entType != null)
               return entType;

            string typeGuid = GUIDUtil.GenerateIFCGuidFrom(elementType, exportType);
            entType = IFCInstanceExporter.CreateGenericIFCType(exportType, elementType, typeGuid, file, null, null);
            productWrapper.RegisterHandleWithElementType(elementType, exportType, entType, null);
         }
         else
         {
            string typeGuid = GUIDUtil.CreateSubElementGUID(element, (int)IFCFamilyInstanceSubElements.InstanceAsType);
            entType = IFCInstanceExporter.CreateGenericIFCType(exportType, element, typeGuid, file, null, null);
         }
         return entType;
      }

      /// Creates a list of IfcCartesianPoints corresponding to a list of UV points that represent a closed boundary loop.
      /// </summary>
      /// <param name="file">The IFCFile.</param>
      /// <param name="projVecData">The list of UV points.</param>
      /// <returns>The corresponding list of IfcCartesianPoints.</returns>
      /// <remarks>We expect that the input UV list is composed of distinct points (i.e., the last point is not the first point, repeated.)
      /// Our output requires that the first IfcCartesianPoint is the same as the last one, and does so by reusing the IfcCartesianPoint handle.</remarks>
      private static IList<IFCAnyHandle> CreateCartesianPointList(IFCFile file, IList<UV> projVecData)
      {
         if (projVecData == null)
            return null;

         // Generate handles for the boundary loop.
         IList<IFCAnyHandle> polyLinePts = new List<IFCAnyHandle>();
         foreach (UV uv in projVecData)
            polyLinePts.Add(CreateCartesianPoint(file, uv));

         // We expect the input to consist of distinct points, i.e., that the first and last points in projVecData are not equal.
         // Our output requires that the first and last points are equal, and as such we reuse the first handle for the last point,
         // to reduce file size and ensure that the endpoints are identically in the same location by sharing the same IfcCartesianPoint reference.
         polyLinePts.Add(polyLinePts[0]);

         return polyLinePts;
      }

      /// <summary>
      /// Call the correct CreateRelDefinesByProperties depending on the schema to create one or more IFC entites.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="guid">The GUID.</param>
      /// <param name="ownerHistory">The owner history.</param>
      /// <param name="name">The name.</param>
      /// <param name="description">The description.</param>
      /// <param name="relatedObjects">The related objects, required to be only 1 for IFC4.</param>
      /// <param name="relatingPropertyDefinition">The property definition to relate to the IFC object entity/entities.</param>
      public static void CreateRelDefinesByProperties(IFCFile file, string guid, IFCAnyHandle ownerHistory,
          string name, string description, ISet<IFCAnyHandle> relatedObjects, IFCAnyHandle relatingPropertyDefinition)
      {
         if (relatedObjects == null)
            return;

         // This code isn't actually valid for IFC4 - IFC4 requires that there be a 1:1 relationship between
         // the one relatedObject and the relatingPropertyDefinition.  This requires a cloning of the IfcPropertySet
         // in addition to cloning the IfcRelDefinesByProperties.  This will be done in the next update.
         IFCInstanceExporter.CreateRelDefinesByProperties(file, guid, ownerHistory, name, description,
             relatedObjects, relatingPropertyDefinition);
      }

      /// <summary>
      /// Call the correct CreateRelDefinesByProperties depending on the schema to create one or more IFC entites.
      /// </summary>
      /// <param name="file">The file.</param>
      /// <param name="ownerHistory">The owner history.</param>
      /// <param name="name">The name.</param>
      /// <param name="description">The description.</param>
      /// <param name="relatedObjects">The related objects, required to be only 1 for IFC4.</param>
      /// <param name="relatingPropertyDefinition">The property definition to relate to the IFC object entity/entities.</param>
      public static void CreateRelDefinesByProperties(IFCFile file, IFCAnyHandle ownerHistory,
          string name, string description, ISet<IFCAnyHandle> relatedObjects, IFCAnyHandle relatingPropertyDefinition)
      {
         if (relatedObjects == null)
            return;

         string guid = GUIDUtil.GenerateIFCGuidFrom(
            GUIDUtil.CreateGUIDString(IFCEntityType.IfcRelDefinesByProperties, name, relatingPropertyDefinition));
         CreateRelDefinesByProperties(file, guid, ownerHistory, name, description, relatedObjects, 
            relatingPropertyDefinition);
      }
      
      /// <summary>
       /// Create an IfcCreateCurveBoundedPlane given a polygonal outer boundary and 0 or more polygonal inner boundaries.
       /// </summary>
       /// <param name="file">The IFCFile.</param>
       /// <param name="newOuterLoopPoints">The list of points representating the outer boundary of the plane.</param>
       /// <param name="innerLoopPoints">The list of inner boundaries of the plane.  This list can be null.</param>
       /// <returns>The IfcCreateCurveBoundedPlane.</returns>
      public static IFCAnyHandle CreateCurveBoundedPlane(IFCFile file, IList<XYZ> newOuterLoopPoints, IList<IList<XYZ>> innerLoopPoints)
      {
         if (newOuterLoopPoints == null)
            return null;

         // We need at least 3 distinct points for the outer polygon.
         int outerSz = newOuterLoopPoints.Count;
         if (outerSz < 3)
            return null;

         // We allow the polygon to duplicate the first and last points, or not.  If the last point is duplicated, we will generally ignore it.
         bool firstIsLast = newOuterLoopPoints[0].IsAlmostEqualTo(newOuterLoopPoints[outerSz - 1]);
         if (firstIsLast && (outerSz == 3))
            return null;

         // Calculate the X direction of the plane using the first point and the next point that generates a valid direction.
         XYZ firstDir = null;
         int ii = 1;
         for (; ii < outerSz; ii++)
         {
            firstDir = (newOuterLoopPoints[ii] - newOuterLoopPoints[0]).Normalize();
            if (firstDir != null)
               break;
         }
         if (firstDir == null)
            return null;

         // Calculate the Y direction of the plane using the first point and the next point that generates a valid direction that isn't
         // parallel to the first direction.
         XYZ secondDir = null;
         for (ii++; ii < outerSz; ii++)
         {
            secondDir = (newOuterLoopPoints[ii] - newOuterLoopPoints[0]).Normalize();
            if (secondDir == null)
               continue;

            if (MathUtil.IsAlmostEqual(Math.Abs(firstDir.DotProduct(secondDir)), 1.0))
               continue;

            break;
         }
         if (secondDir == null)
            return null;

         // Generate the normal of the plane, ensure it is valid.
         XYZ norm = firstDir.CrossProduct(secondDir);
         if (norm == null || norm.IsZeroLength())
            return null;

         norm = norm.Normalize();
         if (norm == null)
            return null;

         // The original secondDir was almost certainly not orthogonal to firstDir; generate an orthogonal direction.
         secondDir = norm.CrossProduct(firstDir);
         firstDir = firstDir.Normalize();
         secondDir = secondDir.Normalize();
         Transform projLCS = GeometryUtil.CreateTransformFromVectorsAndOrigin(firstDir, secondDir, norm, newOuterLoopPoints[0]);

         // If the first and last points are the same, ignore the last point for IFC processing.
         if (firstIsLast)
            outerSz--;

         // Create the UV points before we create handles, to avoid deleting handles on failure.
         IList<UV> projVecData = new List<UV>();
         for (ii = 0; ii < outerSz; ii++)
         {
            UV uv = GeometryUtil.ProjectPointToXYPlaneOfLCS(projLCS, newOuterLoopPoints[ii]);
            if (uv == null)
               return null;
            projVecData.Add(uv);
         }

         // Generate handles for the outer boundary.  This will close the loop by adding the first IfcCartesianPointHandle to the end of polyLinePts.
         IList<IFCAnyHandle> polyLinePts = CreateCartesianPointList(file, projVecData);

         IFCAnyHandle outerBound = IFCInstanceExporter.CreatePolyline(file, polyLinePts);

         IFCAnyHandle origHnd = CreateCartesianPoint(file, newOuterLoopPoints[0]);
         IFCAnyHandle refHnd = CreateDirection(file, firstDir);
         IFCAnyHandle dirHnd = CreateDirection(file, norm);

         IFCAnyHandle positionHnd = IFCInstanceExporter.CreateAxis2Placement3D(file, origHnd, dirHnd, refHnd);
         IFCAnyHandle basisPlane = IFCInstanceExporter.CreatePlane(file, positionHnd);

         // We only assign innerBounds if we create any.  We expect innerBounds to be null if there aren't any created.
         ISet<IFCAnyHandle> innerBounds = null;
         if (innerLoopPoints != null)
         {
            int innerSz = innerLoopPoints.Count;
            for (ii = 0; ii < innerSz; ii++)
            {
               IList<XYZ> currInnerLoopVecData = innerLoopPoints[ii];
               int loopSz = currInnerLoopVecData.Count;
               if (loopSz == 0)
                  continue;

               projVecData.Clear();
               firstIsLast = currInnerLoopVecData[0].IsAlmostEqualTo(currInnerLoopVecData[loopSz - 1]);

               // If the first and last points are the same, ignore the last point for IFC processing.
               if (firstIsLast)
                  loopSz--;

               // Be lenient on what we find, but we need at least 3 distinct points to process an inner polygon.
               bool continueOnFailure = (loopSz < 3);
               for (int jj = 0; jj < loopSz && !continueOnFailure; jj++)
               {
                  UV uv = GeometryUtil.ProjectPointToXYPlaneOfLCS(projLCS, currInnerLoopVecData[jj]);
                  if (uv == null)
                     continueOnFailure = true;
                  else
                     projVecData.Add(uv);
               }

               // We allow for bad inners - we just ignore them.
               if (continueOnFailure)
                  continue;

               // Generate handles for the inner boundary.  This will close the loop by adding the first IfcCartesianPointHandle to the end of polyLinePts.
               polyLinePts = CreateCartesianPointList(file, projVecData);
               IFCAnyHandle polyLine = IFCInstanceExporter.CreatePolyline(file, polyLinePts);

               if (innerBounds == null)
                  innerBounds = new HashSet<IFCAnyHandle>();
               innerBounds.Add(polyLine);
            }
         }

         return IFCInstanceExporter.CreateCurveBoundedPlane(file, basisPlane, outerBound, innerBounds);
      }

      /// <summary>
      /// Creates a copy of the given SolidOrShellTessellationControls object
      /// </summary>
      /// <param name="tessellationControls">The given SolidOrShellTessellationControls object</param>
      /// <returns>The copy of the input object</returns>
      public static SolidOrShellTessellationControls CopyTessellationControls(SolidOrShellTessellationControls tessellationControls)
      {
         SolidOrShellTessellationControls newTessellationControls = new SolidOrShellTessellationControls();

         if (tessellationControls.Accuracy > 0 && tessellationControls.Accuracy <= 30000)
            newTessellationControls.Accuracy = tessellationControls.Accuracy;
         if (tessellationControls.LevelOfDetail >= 0 && tessellationControls.LevelOfDetail <= 1)
            newTessellationControls.LevelOfDetail = tessellationControls.LevelOfDetail;
         if (tessellationControls.MinAngleInTriangle >= 0 && tessellationControls.MinAngleInTriangle < Math.PI / 3)
            newTessellationControls.MinAngleInTriangle = tessellationControls.MinAngleInTriangle;
         if (tessellationControls.MinExternalAngleBetweenTriangles > 0 && tessellationControls.MinExternalAngleBetweenTriangles <= 30000)
            newTessellationControls.MinExternalAngleBetweenTriangles = tessellationControls.MinExternalAngleBetweenTriangles;

         return newTessellationControls;
      }

      /// <summary>
      /// Get tessellation control information for the given element.
      /// </summary>
      /// <param name="element">The element</param>
      /// <param name="tessellationControls">The original tessellation control.</param>
      /// <returns>For some elements, a modified version of the tessellationControls input arugment.  
      /// By default, returns a copy of the original tessellationControls input argument.</returns>
      /// <remarks>This method doesn't alter the tessellationControls input argument.</remarks>
      public static SolidOrShellTessellationControls GetTessellationControl(Element element, SolidOrShellTessellationControls tessellationControls)
      {
         SolidOrShellTessellationControls copyTessellationControls = CopyTessellationControls(tessellationControls);

         Document document = element.Document;
         Element elementType = null;

         //Use the insulations host as the host will have the same shape as the insulation, and then triangulate the insulation. 
         if (element as DuctInsulation != null)
         {
            ElementId hostId = (element as DuctInsulation).HostElementId;

            Element hostElement = document.GetElement(hostId);

            elementType = document.GetElement(hostElement.GetTypeId());

         }
         else
         {
            elementType = document.GetElement(element.GetTypeId());
         }


         if (elementType as FamilySymbol != null)
         {
            FamilySymbol symbol = elementType as FamilySymbol;
            Family family = symbol.Family;
            if (family != null)
            {
               Parameter para = family.GetParameter(ParameterTypeId.FamilyContentPartType);
               if (para != null)
               {
                  if (element as DuctInsulation != null)
                  {
                     copyTessellationControls = GetTessellationControlsForInsulation(copyTessellationControls,
                        ExporterCacheManager.ExportOptionsCache.LevelOfDetail,
                        para.AsInteger());
                  }
                  else
                  {
                     copyTessellationControls = GetTessellationControlsForDuct(copyTessellationControls,
                        ExporterCacheManager.ExportOptionsCache.LevelOfDetail,
                        para.AsInteger());
                  }
               }
            }
         }

         return copyTessellationControls;
      }

      /// <summary>
      /// Returns the tessellation controls with the right setings for an elbow, tee or cross.
      /// </summary>
      /// <param name="controls">The controls to be used in the tessellation</param>
      /// <param name="lod">The level of detail.  </param>
      /// <param name="type">the type of the duct. </param>
      /// <returns>The new SolidOrShellTessellationControls based on the controls input argument.</returns>
      public static SolidOrShellTessellationControls GetTessellationControlsForDuct(SolidOrShellTessellationControls controls,
         ExportOptionsCache.ExportTessellationLevel lod,
         int type)
      {
         // Note that we make no changes of the level of detail is set to High.
         if (type == 5) //Elbow
         {
            switch (lod)
            {
               case ExportOptionsCache.ExportTessellationLevel.ExtraLow:
                  controls.Accuracy = 0.81;
                  controls.LevelOfDetail = 0.05;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.7;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Low:
                  controls.Accuracy = 0.84;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.25;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Medium:
                  controls.Accuracy = 0.74;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.74;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.High:
                  break;
            }
         }
         else if (type == 6) //Tee
         {
            switch (lod)
            {
               case ExportOptionsCache.ExportTessellationLevel.ExtraLow:
                  controls.Accuracy = 1.21;
                  controls.LevelOfDetail = 0.05;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.7;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Low:
                  controls.Accuracy = 0.84;
                  controls.LevelOfDetail = 0.3;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.0;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Medium:
                  controls.Accuracy = 0.84;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.54;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.High:
                  break;
            }
         }
         else if (type == 8) //Cross
         {
            switch (lod)
            {
               case ExportOptionsCache.ExportTessellationLevel.ExtraLow:
                  controls.Accuracy = 0.81;
                  controls.LevelOfDetail = 0.05;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.7;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Low:
                  controls.Accuracy = 0.84;
                  controls.LevelOfDetail = 0.2;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.8;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Medium:
                  controls.Accuracy = 0.81;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.84;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.High:
                  break;
            }
         }
         return controls;
      }


      /// <summary>
      ///  Returns the tessellation controls with the right setings for insulations for a duct of type elbow,tee or cross
      /// </summary>
      /// <param name="controls">The controls to be used in the tessellation</param>
      /// <param name="lod">The level of detail.  </param>
      /// <param name="type">the type of the duct. </param>
      /// <returns>The new SolidOrShellTessellationControls based on the controls input argument.</returns>
      public static SolidOrShellTessellationControls GetTessellationControlsForInsulation(SolidOrShellTessellationControls controls,
         ExportOptionsCache.ExportTessellationLevel lod,
         int type)
      {
         if (type == 5) //Elbow
         {
            switch (lod)
            {
               case ExportOptionsCache.ExportTessellationLevel.ExtraLow:
                  controls.Accuracy = 0.6;
                  controls.LevelOfDetail = 0.1;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.2;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Low:
                  controls.Accuracy = 0.6;
                  controls.LevelOfDetail = 0.3;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.7;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Medium:
                  controls.Accuracy = 0.5;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.35;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.High:
                  break;
            }
         }
         else if (type == 6) //Tee
         {
            switch (lod)
            {
               case ExportOptionsCache.ExportTessellationLevel.ExtraLow:
                  controls.Accuracy = 0.6;
                  controls.LevelOfDetail = 0.1;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.2;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Low:
                  controls.Accuracy = 0.6;
                  controls.LevelOfDetail = 0.2;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.9;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Medium:
                  controls.Accuracy = 0.5;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.55;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.High:
                  break;
            }
         }
         else if (type == 8) //Cross
         {
            switch (lod)
            {
               case ExportOptionsCache.ExportTessellationLevel.ExtraLow:
                  controls.Accuracy = 0.6;
                  controls.LevelOfDetail = 0.1;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 1.2;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Low:
                  controls.Accuracy = 0.6;
                  controls.LevelOfDetail = 0.2;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.9;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.Medium:
                  controls.Accuracy = 0.5;
                  controls.LevelOfDetail = 0.4;
                  controls.MinAngleInTriangle = 0.13;
                  controls.MinExternalAngleBetweenTriangles = 0.55;
                  break;
               case ExportOptionsCache.ExportTessellationLevel.High:
                  break;
            }
         }
         return controls;
      }

      /// <summary>
      /// Get information about single material.
      /// </summary>
      /// <param name="exporterIFC">the exporter IFC</param>
      /// <param name="element">the element</param>
      /// <param name="matIds">material ids (out)</param>
      /// <returns>the handle</returns>
      public static IFCAnyHandle GetSingleMaterial(ExporterIFC exporterIFC, Element element, ElementId matId)
      {
         string paramValue;
         IFCAnyHandle singleMaterialOverrideHnd = null;

         ParameterUtil.GetStringValueFromElementOrSymbol(element, "IfcSingleMaterialOverride", out paramValue);
         if (!string.IsNullOrEmpty(paramValue))
         {
            singleMaterialOverrideHnd = IFCInstanceExporter.CreateMaterial(exporterIFC.GetFile(), paramValue, null, null);
            ExporterCacheManager.MaterialHandleCache.Register(matId, singleMaterialOverrideHnd);
         }
         return singleMaterialOverrideHnd;
      }

      /// <summary>
      /// Gets material name from element's IfcSingleMaterialOverride parameter
      /// and searches for Material with equal name in Document.
      /// </summary>
      /// <param name="element">the element</param>
      /// <returns>Material ID</returns>
      public static ElementId GetSingleMaterial(Element element)
      {
         ElementId matID = ElementId.InvalidElementId;

         string matName;
         ParameterUtil.GetStringValueFromElementOrSymbol(element, "IfcSingleMaterialOverride", out matName);
         if (!string.IsNullOrEmpty(matName))
         {
            Material mat = new FilteredElementCollector(element.Document)
                        .WhereElementIsNotElementType()
                        .OfClass(typeof(Material))
                        .Where(m => m.Name == matName)
                        .Cast<Material>()
                        .FirstOrDefault();
            if (mat != null)
               matID = mat.Id;
         }

         return matID;
      }

      /// <summary>
      /// Get Transform from an IfcLocalPlacement
      /// </summary>
      /// <param name="ecsHnd">Handle to the IfcLocalPlacement</param>
      /// <returns>Transform from the RelativePlacement attribute of the IfcLocalPlacement</returns>
      public static Transform GetTransformFromLocalPlacementHnd(IFCAnyHandle ecsHnd)
      {
         Transform ecsFromHnd = null;
         if (!IFCAnyHandleUtil.IsTypeOf(ecsHnd, IFCEntityType.IfcLocalPlacement))
            return null;

         IFCAnyHandle relPlacement = IFCAnyHandleUtil.GetInstanceAttribute(ecsHnd, "RelativePlacement");       // expected: IfcAxis2Placement3D
         if (!IFCAnyHandleUtil.IsTypeOf(relPlacement, IFCEntityType.IfcAxis2Placement3D))
            return null;

         IFCAnyHandle zDir = IFCAnyHandleUtil.GetInstanceAttribute(relPlacement, "Axis");                      // IfcDirection
         IFCAnyHandle xDir = IFCAnyHandleUtil.GetInstanceAttribute(relPlacement, "RefDirection");              // IfcDirection
         IFCAnyHandle pos = IFCAnyHandleUtil.GetInstanceAttribute(relPlacement, "Location");                   // IfcCartesianPoint

         XYZ xDirection = null;
         XYZ zDirection = null;

         if (zDir != null)
         {
            IList<double> zDirValues = IFCAnyHandleUtil.GetAggregateDoubleAttribute<List<double>>(zDir, "DirectionRatios");
            zDirection = new XYZ(zDirValues[0], zDirValues[1], zDirValues[2]);
         }
         else
         {
            // Default Z-Direction
            zDirection = new XYZ(0.0, 0.0, 1.0);
         }

         if (xDir != null)
         {
            IList<double> xDirValues = IFCAnyHandleUtil.GetAggregateDoubleAttribute<List<double>>(xDir, "DirectionRatios");
            xDirection = new XYZ(xDirValues[0], xDirValues[1], xDirValues[2]);
         }
         else
         {
            // Default X-Direction
            xDirection = new XYZ(1.0, 0.0, 0.0);
         }

         XYZ yDirection = zDirection.CrossProduct(xDirection);
         IList<double> posCoords = IFCAnyHandleUtil.GetAggregateDoubleAttribute<List<double>>(pos, "Coordinates");
         XYZ position = new XYZ(posCoords[0], posCoords[1], posCoords[2]);

         ecsFromHnd = Transform.Identity;
         ecsFromHnd.BasisX = xDirection;
         ecsFromHnd.BasisY = yDirection;
         ecsFromHnd.BasisZ = zDirection;
         ecsFromHnd.Origin = position;

         return ecsFromHnd;
      }

      /// <summary>
      /// Compute the total tansform of a local placement
      /// </summary>
      /// <param name="localPlacementHnd">the local placement handle</param>
      /// <returns>the resulting total transform</returns>
      public static Transform GetTotalTransformFromLocalPlacement(IFCAnyHandle localPlacementHnd)
      {
         Transform totalTrf = Transform.Identity;

         if (IFCAnyHandleUtil.IsNullOrHasNoValue(localPlacementHnd))
            return totalTrf;

         if (!localPlacementHnd.IsTypeOf("IfcLocalPlacement"))
            return totalTrf;

         totalTrf = GetTransformFromLocalPlacementHnd(localPlacementHnd);

         IFCAnyHandle placementRelTo = IFCAnyHandleUtil.GetInstanceAttribute(localPlacementHnd, "PlacementRelTo");
         while (!IFCAnyHandleUtil.IsNullOrHasNoValue(placementRelTo))
         {
            Transform trf = GetTransformFromLocalPlacementHnd(placementRelTo);
            if (trf == null)
               return null;        // the placementRelTo is not the type of IfcLocalPlacement, return null. We don't handle this

            totalTrf = trf.Multiply(totalTrf);
            placementRelTo = IFCAnyHandleUtil.GetInstanceAttribute(placementRelTo, "PlacementRelTo");
         }

         return totalTrf;
      }

      /// <summary>
      /// Simple scaling of Transform from scaled unit (used in IFC) to the internal unscaled Revit tansform
      /// </summary>
      /// <param name="scaledTrf">scaled Transform</param>
      /// <returns>unscaled Transform</returns>
      public static Transform UnscaleTransformOrigin(Transform scaledTrf)
      {
         Transform unscaledTrf = new Transform(scaledTrf);
         unscaledTrf.Origin = UnitUtil.UnscaleLength(scaledTrf.Origin);
         return unscaledTrf;
      }

      /// <summary>
      /// Simple scaling of Transform from the Revit internal value to the IFC scaled unit
      /// </summary>
      /// <param name="unscaledTrf">the unscaled Transform</param>
      /// <returns>scaled Transform</returns>
      public static Transform ScaleTransformOrigin(Transform unscaledTrf)
      {
         Transform scaledTrf = new Transform(unscaledTrf);
         scaledTrf.Origin = UnitUtil.ScaleLength(unscaledTrf.Origin);
         return scaledTrf;
      }

      public static ISet<IFCAnyHandle> CleanRefObjects(ISet<IFCAnyHandle> cacheHandles)
      {
         if (cacheHandles == null)
            return null;

         IList<IFCAnyHandle> refObjToDel = new List<IFCAnyHandle>();
         foreach (IFCAnyHandle cacheHandle in cacheHandles)
         {
            if (ExporterCacheManager.HandleToDeleteCache.Contains(cacheHandle))
            {
               refObjToDel.Add(cacheHandle);
            }
            else if (IFCAnyHandleUtil.IsNullOrHasNoValue(cacheHandle))
            {
               // If we get to these lines of code, then there is an error somewhere
               // where we deleted a handle but didn't properly mark it as deleted.
               // This should be investigated, but this will at least not prevent
               // the export.
               ExporterCacheManager.HandleToDeleteCache.Add(cacheHandle);
               refObjToDel.Add(cacheHandle);
            }
         }

         foreach (IFCAnyHandle refObjHandle in refObjToDel)
         {
            cacheHandles.Remove(refObjHandle);
         }

         return cacheHandles;
      }

      /// <summary>
      /// Add into the Complex property cache for the product handle. This will be used later when the complex Quantities are created
      /// </summary>
      /// <param name="productHandle">the product handle</param>
      /// <param name="layersetInfo">the layersetinfo</param>
      public static bool AddIntoComplexPropertyCache(IFCAnyHandle productHandle, MaterialLayerSetInfo layersetInfo)
      {
         // export Width Base Quantities if it is IFC4RV and the data is available
         if (ExporterCacheManager.ExportOptionsCache.ExportAs4ReferenceView && ExporterCacheManager.ExportOptionsCache.ExportBaseQuantities)
         {
            if (layersetInfo != null && layersetInfo.LayerQuantityWidthHnd != null && layersetInfo.LayerQuantityWidthHnd.Count > 0)
            {
               // Add to cache to be assigned later when processing the Quantities
               ExporterCacheManager.ComplexPropertyCache.Add(productHandle, layersetInfo.LayerQuantityWidthHnd);
               return true;
            }
         }
         return false;
      }

      public enum ExportPartAs
      {
         Part,
         ShapeAspect,
         None
      }
      /// <summary>
      /// Check whether an element should be exported as its components or parts
      /// </summary>
      /// <param name="element">the element</param>
      /// <param name="layersOrPartsCount">number of element's layers or associated parts</param>
      /// <returns>whether it shoudl be exported by components</returns>
      public static ExportPartAs ShouldExportByComponentsOrParts(Element element, int layersOrPartsCount)
      {
         bool exportParts = PartExporter.ShouldExportParts(element, layersOrPartsCount);
         if(exportParts)
            return ExportPartAs.Part;

         if (ExporterCacheManager.ExportOptionsCache.ExportAs4ReferenceView && !exportParts && layersOrPartsCount > 1)
         {
            return ExportPartAs.ShapeAspect;
         }

         return ExportPartAs.None;
      }

      /// <summary>
      /// Checks if elemnt has associated parts and all conditions are met for exporting it as Components or Parts.
      /// </summary>
      /// <param name="element">the element</param>
      /// <returns>whether it can be exported by components or parts</returns>
      public static ExportPartAs CanExportByComponentsOrParts(Element element)
      {
         ExportPartAs exportPartAs = ShouldExportByComponentsOrParts(element, PartUtils.GetAssociatedParts(element.Document, element.Id, false, true).Count);
         if(PartUtils.HasAssociatedParts(element.Document, element.Id) && (exportPartAs == ExportPartAs.Part || exportPartAs == ExportPartAs.ShapeAspect))
         {
            return exportPartAs;
         }

         return ExportPartAs.None;
      }

      /// <summary>
      /// Creates parts for element if it is possible.
      /// </summary>
      /// <param name="element">the element</param>
      /// <param name="layersCount">number of element's layers or associated parts</param>
      /// <returns>true - if parts have been successfully created. false - is creation of parts is not possible.</returns>
      public static bool CreateParts(Element element, int layersCount, ref GeometryElement geometryElement)
      {
         ExportPartAs exportPartAs = ShouldExportByComponentsOrParts(element, layersCount);

         if (ExporterCacheManager.ExportOptionsCache.ExportAs4ReferenceView && (exportPartAs == ExportPartAs.Part || exportPartAs == ExportPartAs.ShapeAspect))
         {
            Document doc = element.Document;
            ICollection<ElementId> ids = new List<ElementId>() { element.Id };
            if (PartUtils.AreElementsValidForCreateParts(doc, ids))
            {
               PartUtils.CreateParts(doc, ids);
               doc.Regenerate();

               //geometryElement is either re-acquired or set to null here because call to PartUtils.CreateParts() and doc.Regenerate() can invalidate its value.
               if (exportPartAs == ExportPartAs.ShapeAspect)
                  //If we export Shape Aspects we will also export original geometry because Shape Aspects reference it.
                  //When exporting original geometry the code will use GeometryElement so get it now.
                  geometryElement = element.get_Geometry(GeometryUtil.GetIFCExportGeometryOptions());
               else
                  //If we export Parts we do not need to export original geometry so set geometryElement to null.
                  geometryElement = null;
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Checks if ElementId corresponds to an Element in a Document.
      /// </summary>
      /// <param name="id">the element id</param>
      /// <returns>true - if id is built in or invalid, false - otherwise</returns>
      public static bool IsElementIdBuiltInOrInvalid(ElementId id)
      {
         return id <= ElementId.InvalidElementId;
      }
   }
}