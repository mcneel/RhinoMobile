//
// ModelInstanceRef.cs
// RhinoMobile.Model
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Collections.Generic;

using Rhino.Geometry;
using RhinoMobile.Model;
using RhinoMobile.Display;

namespace RhinoMobile.Model
{
	public class ModelInstanceRef : ModelObject
	{
		#region members
		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		Transform m_xform;
		Guid m_definitionGUID;
		#endregion

		#region constructors
		/// <summary>
		/// Creates an instance of a ModelInstanceRef based on the definition with a new transform.
		/// </summary>
		public ModelInstanceRef (Guid guid, Guid defGUID, Transform xform)
		{
			ObjectId = guid;
			m_definitionGUID = defGUID;
			m_xform = xform;
		}
		#endregion

		#region methods
		/// <summary>
		/// Calls into this reference's ModelInstanceDef ExplodeIntoArray method
		/// </summary>
		public new void ExplodeIntoArray (RMModel model, List<DisplayObject> array, Transform initialXform)
		{
			Transform newXform = initialXform * m_xform;
			RMModel currentModel = model;
			ModelObject modelObject = currentModel.ModelObjectWithGUID (m_definitionGUID);
			(modelObject as ModelInstanceDef).LayerIndex = this.LayerIndex;
			(modelObject as ModelInstanceDef).ExplodeIntoArray (model, array, newXform);	
		}
		#endregion

	}
}