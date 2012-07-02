﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MySQL.Utility;
using System.Collections;
using MySql.Data.MySqlClient;
using System.Reflection;

namespace MySQL.ForExcel
{
  public partial class ImportRoutineForm : AutoStyleableBaseDialog
  {
    private MySqlWorkbenchConnection wbConnection;
    private DBObject importDBObject;
    private PropertiesCollection routineParamsProperties;
    private MySqlParameter[] mysqlParameters;
    public DataSet ImportDataSet = null;
    public bool ImportHeaders { get { return chkIncludeHeaders.Checked; } }
    public ImportMultipleType ImportType
    {
      get
      {
        ImportMultipleType retType = ImportMultipleType.SingleWorkSheetHorizontally;
        int multTypeValue = (cmbMultipleResultSets != null && cmbMultipleResultSets.Items.Count > 0 ? (int)cmbMultipleResultSets.SelectedValue : 0);
        switch (multTypeValue)
        {
          case 0:
            retType = ImportMultipleType.SingleWorkSheetHorizontally;
            break;
          case 1:
            retType = ImportMultipleType.SingleWorkSheetVertically;
            break;
          case 2:
            retType = ImportMultipleType.MultipleWorkSheets;
            break;
        }
        return retType;
      }
    }

    public ImportRoutineForm(MySqlWorkbenchConnection wbConnection, DBObject importDBObject)
    {
      this.wbConnection = wbConnection;
      this.importDBObject = importDBObject;

      InitializeComponent();

      routineParamsProperties = new PropertiesCollection();
      lblFromRoutineName.Text = importDBObject.Name;
      parametersGrid.SelectedObject = routineParamsProperties;

      initializeMultipleResultSetsCombo();
      fillParameters();
      chkIncludeHeaders.Checked = true;
    }

    private void initializeMultipleResultSetsCombo()
    {
      DataTable dt = new DataTable();
      dt.Columns.Add("value", Type.GetType("System.Int32"));
      dt.Columns.Add("description", Type.GetType("System.String"));
      DataRow dr = dt.NewRow();
      dr["value"] = ImportMultipleType.SingleWorkSheetHorizontally;
      dr["description"] = "Single WorkSheet Horizontally";
      dt.Rows.Add(dr);
      dr = dt.NewRow();
      dr["value"] = ImportMultipleType.SingleWorkSheetVertically;
      dr["description"] = "Single WorkSheet Vertically";
      dt.Rows.Add(dr);
      dr = dt.NewRow();
      dr["value"] = ImportMultipleType.MultipleWorkSheets;
      dr["description"] = "Multiple WorkSheets";
      dt.Rows.Add(dr);
      cmbMultipleResultSets.DataSource = dt;
      cmbMultipleResultSets.DisplayMember = "description";
      cmbMultipleResultSets.ValueMember = "value";
    }

    private void fillParameters()
    {
      CustomProperty parameter = null;
      DataTable parametersTable = Utilities.GetSchemaCollection(wbConnection, "Procedure Parameters", null, wbConnection.Schema, importDBObject.Name);
      mysqlParameters = new MySqlParameter[parametersTable.Rows.Count];
      int paramIdx = 0;
      MySqlDbType dbType = MySqlDbType.Guid;
      object objValue = null;

      foreach (DataRow dr in parametersTable.Rows)
      {
        string dataType = dr["DATA_TYPE"].ToString().ToLowerInvariant();
        string paramName = dr["PARAMETER_NAME"].ToString();
        ParameterDirection paramDirection = ParameterDirection.Input;
        int paramSize = (dr["CHARACTER_MAXIMUM_LENGTH"] != null && dr["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value ? Convert.ToInt32(dr["CHARACTER_MAXIMUM_LENGTH"]) : 0);
        byte paramPrecision = (dr["NUMERIC_PRECISION"] != null && dr["NUMERIC_PRECISION"] != DBNull.Value ? Convert.ToByte(dr["NUMERIC_PRECISION"]) : (byte)0);
        byte paramScale = (dr["NUMERIC_SCALE"] != null && dr["NUMERIC_SCALE"] != DBNull.Value ? Convert.ToByte(dr["NUMERIC_SCALE"]) : (byte)0);
        bool paramUnsigned = dr["DTD_IDENTIFIER"].ToString().Contains("unsigned");
        //string paramDirectionStr = (dr["PARAMETER_MODE"] != null && dr["PARAMETER_MODE"] != DBNull.Value ? dr["PARAMETER_MODE"].ToString().ToLowerInvariant() : "return");
        string paramDirectionStr = (paramName != "RETURN_VALUE" ? dr["PARAMETER_MODE"].ToString().ToLowerInvariant() : "return");
        bool paramIsReadOnly = false;

        switch (paramDirectionStr)
        {
          case "in":
            paramDirection = ParameterDirection.Input;
            paramIsReadOnly = false;
            break;
          case "out":
            paramDirection = ParameterDirection.Output;
            paramIsReadOnly = true;
            break;
          case "inout":
            paramDirection = ParameterDirection.InputOutput;
            paramIsReadOnly = false;
            break;
          case "return":
            paramDirection = ParameterDirection.ReturnValue;
            paramIsReadOnly = true;
            break;
        }

        switch (dataType)
        {
          case "bit":
            dbType = MySqlDbType.Bit;
            if (paramPrecision > 1)
              objValue = 0;
            else
              objValue = false;
            break;
          case "int":
          case "integer":
            dbType = MySqlDbType.Int32;
            objValue = (Int32)0;
            break;
          case "tinyint":
            dbType = (paramUnsigned ? MySqlDbType.UByte : MySqlDbType.Byte);
            objValue = (Byte)0;
            break;
          case "smallint":
            dbType = (paramUnsigned ? MySqlDbType.UInt16 : MySqlDbType.Int16);
            objValue = (Int16)0;
            break;
          case "mediumint":
            dbType = (paramUnsigned ? MySqlDbType.UInt24 : MySqlDbType.Int24);
            objValue = (Int32)0;
            break;
          case "bigint":
            dbType = (paramUnsigned ? MySqlDbType.UInt64 : MySqlDbType.Int64);
            objValue = (Int64)0;
            break;
          case "numeric":
          case "decimal":
          case "float":
          case "double":
          case "real":
            dbType = (dataType == "float" ? MySqlDbType.Float : (dataType == "double" || dataType == "real" ? MySqlDbType.Double : MySqlDbType.Decimal));
            objValue = (Double)0;
            break;
          case "char":
          case "varchar":
            dbType = MySqlDbType.VarChar;
            objValue = String.Empty;
            break;
          case "binary":
          case "varbinary":
            dbType = (dataType.StartsWith("var") ? MySqlDbType.VarBinary : MySqlDbType.Binary);
            objValue = String.Empty;
            break;
          case "text":
          case "tinytext":
          case "mediumtext":
          case "longtext":
            dbType = (dataType.StartsWith("var") ? MySqlDbType.VarBinary : MySqlDbType.Binary);
            objValue = String.Empty;
            break;
          case "date":
          case "datetime":
          case "timestamp":
            dbType = (dataType == "date" ? MySqlDbType.Date : MySqlDbType.DateTime);
            objValue = DateTime.Today;
            break;
          case "time":
            dbType = MySqlDbType.Time;
            objValue = TimeSpan.Zero;
            break;
          case "blob":
            dbType = MySqlDbType.Blob;
            objValue = null;
            break;
        }
        parameter = new CustomProperty(paramName, objValue, paramIsReadOnly, true);
        parameter.Description = String.Format("Direction: {0}, Data Type: {1}", paramDirection.ToString(), dataType);
        mysqlParameters[paramIdx] = new MySqlParameter(paramName, dbType, paramSize, paramDirection, false, paramPrecision, paramScale, null, DataRowVersion.Current, objValue);
        routineParamsProperties.Add(parameter);
        paramIdx++;
      }
      FieldInfo fi = parametersGrid.GetType().GetField("gridView", BindingFlags.NonPublic | BindingFlags.Instance);
      object gridViewRef = fi.GetValue(parametersGrid);
      Type gridViewType = gridViewRef.GetType();
      MethodInfo mi = gridViewType.GetMethod("MoveSplitterTo", BindingFlags.NonPublic | BindingFlags.Instance);
      int gridColWidth = (int)Math.Truncate(parametersGrid.Width * 0.4);
      mi.Invoke(gridViewRef, new object[] { gridColWidth });
      parametersGrid.Refresh();
    }

    private void btnCall_Click(object sender, EventArgs e)
    {
      // Prepare parameters and execute routine and create OutAndReturnValues table
      DataTable outParamsTable = new DataTable("OutAndReturnValues");
      for (int paramIdx = 0; paramIdx < routineParamsProperties.Count; paramIdx++)
      {
        mysqlParameters[paramIdx].Value = routineParamsProperties[paramIdx].Value;
        if (mysqlParameters[paramIdx].Direction == ParameterDirection.Output || mysqlParameters[paramIdx].Direction == ParameterDirection.ReturnValue)
          outParamsTable.Columns.Add(routineParamsProperties[paramIdx].Name, routineParamsProperties[paramIdx].Value.GetType());
      }
      ImportDataSet = Utilities.GetDataSetFromRoutine(wbConnection, importDBObject, mysqlParameters);
      if (ImportDataSet == null || ImportDataSet.Tables.Count == 0)
      {
        btnImport.Enabled = false;
        return;
      }
      btnImport.Enabled = true;

      // Refresh output/return parameter values in PropertyGrid and add them to OutAndReturnValues table
      DataRow valuesRow = outParamsTable.NewRow();
      for (int paramIdx = 0; paramIdx < routineParamsProperties.Count; paramIdx++)
      {
        if (mysqlParameters[paramIdx].Direction == ParameterDirection.Output || mysqlParameters[paramIdx].Direction == ParameterDirection.ReturnValue)
        {
          routineParamsProperties[paramIdx].Value = mysqlParameters[paramIdx].Value;
          valuesRow[mysqlParameters[paramIdx].ParameterName] = mysqlParameters[paramIdx].Value;
        }
      }
      outParamsTable.Rows.Add(valuesRow);
      ImportDataSet.Tables.Add(outParamsTable);
      parametersGrid.Refresh();

      // Refresh list of ResultSets
      lisResultSets.Items.Clear();
      for (int dtIdx = 0; dtIdx < ImportDataSet.Tables.Count; dtIdx++)
      {
        lisResultSets.Items.Add(ImportDataSet.Tables[dtIdx].TableName);
      }
      if (lisResultSets.Items.Count > 0)
        lisResultSets.SelectedIndex = 0;
    }

    private void lisResultSets_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (lisResultSets.Items.Count > 0)
      {
        grdPreviewData.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grdPreviewData.DataSource = ImportDataSet;
        grdPreviewData.DataMember = ImportDataSet.Tables[lisResultSets.SelectedIndex].TableName;
        foreach (DataGridViewColumn gridCol in grdPreviewData.Columns)
        {
          gridCol.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        grdPreviewData.SelectionMode = DataGridViewSelectionMode.FullColumnSelect;
      }
    }
  }

  public enum ImportMultipleType
  {
    SingleWorkSheetHorizontally, SingleWorkSheetVertically, MultipleWorkSheets
  };

  public class CustomProperty
  {
    public string Name { get; private set; }
    public bool ReadOnly { get; private set; }
    public bool Visible { get; private set; }
    public object Value { get; set; }
    public string Description { get; set; }

    public CustomProperty(string sName, object value, bool bReadOnly, bool bVisible)
    {
      Name = sName;
      Value = value;
      ReadOnly = bReadOnly;
      Visible = bVisible;
    }
  }

  public class CustomPropertyDescriptor : PropertyDescriptor
  {
    CustomProperty m_Property;
    public CustomPropertyDescriptor(ref CustomProperty myProperty, Attribute[] attrs)
      : base(myProperty.Name, attrs)
    {
      m_Property = myProperty;
    }

    #region PropertyDescriptor specific

    public override bool CanResetValue(object component)
    {
      return false;
    }

    public override Type ComponentType
    {
      get { return null; }
    }

    public override object GetValue(object component)
    {
      return m_Property.Value;
    }

    public override string Description
    {
      get { return m_Property.Description; }
    }

    public override string Category
    {
      get { return String.Empty; }
    }

    public override string DisplayName
    {
      get { return m_Property.Name; }
    }

    public override bool IsReadOnly
    {
      get { return m_Property.ReadOnly; }
    }

    public override void ResetValue(object component)
    {
      //Have to implement
    }

    public override bool ShouldSerializeValue(object component)
    {
      return false;
    }

    public override void SetValue(object component, object value)
    {
      m_Property.Value = value;
    }

    public override Type PropertyType
    {
      get { return m_Property.Value.GetType(); }
    }

    #endregion
  }

  public class PropertiesCollection : CollectionBase, ICustomTypeDescriptor
  {
    public CustomProperty this[int index]
    {
      get { return (CustomProperty)base.List[index]; }
      set { base.List[index] = (CustomProperty)value; }
    }

    public void Add(CustomProperty value)
    {
      base.List.Add(value);
    }

    public void Remove(string name)
    {
      foreach (CustomProperty prop in base.List)
      {
        if (prop.Name == name)
        {
          base.List.Remove(prop);
          return;
        }
      }
    }

    #region TypeDescriptor Implementation

    public String GetClassName()
    {
      return TypeDescriptor.GetClassName(this, true);
    }

    public AttributeCollection GetAttributes()
    {
      return TypeDescriptor.GetAttributes(this, true);
    }

    public String GetComponentName()
    {
      return TypeDescriptor.GetComponentName(this, true);
    }

    public TypeConverter GetConverter()
    {
      return TypeDescriptor.GetConverter(this, true);
    }

    public EventDescriptor GetDefaultEvent()
    {
      return TypeDescriptor.GetDefaultEvent(this, true);
    }

    public PropertyDescriptor GetDefaultProperty()
    {
      return TypeDescriptor.GetDefaultProperty(this, true);
    }

    public object GetEditor(Type editorBaseType)
    {
      return TypeDescriptor.GetEditor(this, editorBaseType, true);
    }

    public EventDescriptorCollection GetEvents(Attribute[] attributes)
    {
      return TypeDescriptor.GetEvents(this, attributes, true);
    }

    public EventDescriptorCollection GetEvents()
    {
      return TypeDescriptor.GetEvents(this, true);
    }

    public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      PropertyDescriptor[] newProps = new PropertyDescriptor[this.Count];
      for (int i = 0; i < this.Count; i++)
      {
        CustomProperty prop = (CustomProperty)this[i];
        newProps[i] = new CustomPropertyDescriptor(ref prop, attributes);
      }
      return new PropertyDescriptorCollection(newProps);
    }

    public PropertyDescriptorCollection GetProperties()
    {
      return TypeDescriptor.GetProperties(this, true);
    }

    public object GetPropertyOwner(PropertyDescriptor pd)
    {
      return this;
    }

    #endregion TypeDescriptor Implementation
  }

}