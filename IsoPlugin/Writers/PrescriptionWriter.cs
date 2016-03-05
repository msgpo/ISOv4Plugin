﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AgGateway.ADAPT.ApplicationDataModel.Common;
using AgGateway.ADAPT.ApplicationDataModel.Logistics;
using AgGateway.ADAPT.ApplicationDataModel.Prescriptions;
using AgGateway.ADAPT.ApplicationDataModel.Representations;
using AgGateway.ADAPT.Representation.UnitSystem.ExtensionMethods;

namespace AgGateway.ADAPT.IsoPlugin.Writers
{
    internal class PrescriptionWriter : BaseWriter
    {
        private GridWriter _gridWriter;
        private Representation.UnitSystem.UnitOfMeasureConverter _unitConverter;

        private PrescriptionWriter(TaskDocumentWriter taskWriter)
            : base(taskWriter, "TSK")
        {
            _unitConverter = new Representation.UnitSystem.UnitOfMeasureConverter();
            _gridWriter = new GridWriter(taskWriter);
        }

        internal static void Write(TaskDocumentWriter taskWriter)
        {
            if (taskWriter.DataModel.Catalog.Prescriptions == null ||
                taskWriter.DataModel.Catalog.Prescriptions.Count == 0)
                return;

            var writer = new PrescriptionWriter(taskWriter);
            writer.Write();
        }

        private void Write()
        {
            WriteToExternalFile(WritePrescriptions);
        }

        private void WritePrescriptions(XmlWriter writer)
        {
            foreach (var prescription in TaskWriter.DataModel.Catalog.Prescriptions.OfType<RasterGridPrescription>())
            {
                WritePrescription(writer, prescription);
            }
        }

        private void WritePrescription(XmlWriter writer, RasterGridPrescription prescription)
        {
            if (!IsValidPrescription(prescription))
                return;

            writer.WriteStartElement(XmlPrefix);
            writer.WriteAttributeString("A", GenerateId());
            writer.WriteAttributeString("B", prescription.Description);

            WriteFieldMeta(writer, prescription.FieldId);

            // Task status - planned
            writer.WriteAttributeString("G", "1");

            var defaultTreatmentZone = WriteTreatmentZones(writer, prescription);

            _gridWriter.Write(writer, prescription, defaultTreatmentZone);

            writer.WriteEndElement();
        }

        private static bool IsValidPrescription(RasterGridPrescription prescription)
        {
            return prescription.Rates != null &&
                prescription.CellHeight != null &&
                prescription.CellWidth != null &&
                prescription.Origin != null;
        }

        private void WriteFieldMeta(XmlWriter writer, int fieldId)
        {
            var field = TaskWriter.Fields.FindById(fieldId);
            writer.WriteXmlAttribute("E", field);

            if (!string.IsNullOrEmpty(field))
                WriteFarmMeta(writer, fieldId);

        }

        private void WriteFarmMeta(XmlWriter writer, int fieldId)
        {
            foreach (var field in TaskWriter.DataModel.Catalog.Fields)
            {
                if (field.Id.ReferenceId == fieldId)
                {
                    if (field.FarmId.HasValue)
                    {
                        var farmId = TaskWriter.Farms.FindById(field.FarmId.Value);
                        writer.WriteXmlAttribute("D", farmId);

                        if (!string.IsNullOrEmpty(farmId))
                            WriteCustomerMeta(writer, field.FarmId.Value);
                    }
                    break;
                }
            }
        }

        private void WriteCustomerMeta(XmlWriter writer, int farmId)
        {
            foreach (var farm in TaskWriter.DataModel.Catalog.Farms)
            {
                if (farm.Id.ReferenceId == farmId)
                {
                    if (farm.GrowerId.HasValue)
                    {
                        var customerId = TaskWriter.Customers.FindById(farm.GrowerId.Value);
                        writer.WriteXmlAttribute("C", customerId);
                    }
                    break;
                }
            }
        }

        private TreatmentZone WriteTreatmentZones(XmlWriter writer, RasterGridPrescription prescription)
        {
            if (prescription.ProductIds == null)
                return null;

            var lossOfSignlaTreatmentZone = new TreatmentZone { Name = "Loss of GPS", Variables = new List<DataVariable>() };
            var outOfFieldTreatmentZone = new TreatmentZone { Name = "Out of Field", Variables = new List<DataVariable>() };
            var defaultTreatmentZone = new TreatmentZone { Name = "Default", Variables = new List<DataVariable>() };

            var defaultRate = new NumericRepresentationValue(null, new NumericValue(prescription.RateUnit, 0));
            var isoUnit = DetermineIsoUnit(prescription.RateUnit);

            foreach (var productId in prescription.ProductIds)
            {
                var isoProductId = TaskWriter.Products.FindById(productId);

                AddDataVariable(lossOfSignlaTreatmentZone, prescription.LossOfGpsRate, isoProductId, isoUnit);
                AddDataVariable(outOfFieldTreatmentZone, prescription.OutOfFieldRate, isoProductId, isoUnit);
                AddDataVariable(defaultTreatmentZone, defaultRate, isoProductId, isoUnit);
            }

            var lossOfSignalZoneId = "253";
            if (lossOfSignlaTreatmentZone.Variables.Count > 0)
                writer.WriteXmlAttribute("I", lossOfSignalZoneId);

            var outOfFieldZoneId = "254";
            if (outOfFieldTreatmentZone.Variables.Count > 0)
                writer.WriteXmlAttribute("J", outOfFieldZoneId);

            TreatmentZoneWriter.Write(writer, "1", defaultTreatmentZone);
            if (lossOfSignlaTreatmentZone.Variables.Count > 0)
                TreatmentZoneWriter.Write(writer, lossOfSignalZoneId, lossOfSignlaTreatmentZone);
            if (outOfFieldTreatmentZone.Variables.Count > 0)
                TreatmentZoneWriter.Write(writer, outOfFieldZoneId, outOfFieldTreatmentZone);

            return defaultTreatmentZone;
        }

        private static IsoUnit DetermineIsoUnit(UnitOfMeasure rateUnit)
        {
            if (rateUnit == null)
                return null;

            return UnitFactory.Instance.GetUnitByDimension(rateUnit.Dimension);
        }

        private void AddDataVariable(TreatmentZone treatmentZone, NumericRepresentationValue value,
            string productId, IsoUnit unit)
        {
            if (value != null && value.Value != null)
            {
                var targetValue = value.Value.Value;

                // Convert input value to Iso unit
                var adaptUnit = unit.ToAdaptUnit();
                UnitOfMeasure userUnit = null;
                if (adaptUnit != null && value.Value.UnitOfMeasure != null &&
                    adaptUnit.Dimension == value.Value.UnitOfMeasure.Dimension)
                {
                    userUnit = value.Value.UnitOfMeasure;
                    targetValue = _unitConverter.Convert(userUnit.ToInternalUom(), adaptUnit.ToInternalUom(), targetValue);
                }

                var dataVariable = new DataVariable
                {
                    ProductId = productId,
                    Value = targetValue,
                    IsoUnit = unit,
                    UserUnit = userUnit
                };

                treatmentZone.Variables.Add(dataVariable);
            }
        }
    }
}