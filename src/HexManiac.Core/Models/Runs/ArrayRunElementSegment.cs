﻿using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public enum ElementContentType {
      Unknown,
      PCS,
      Integer,
      Pointer,
      BitArray,
   }

   public class ArrayRunElementSegment {
      public string Name { get; }
      public ElementContentType Type { get; }
      public int Length { get; }
      public ArrayRunElementSegment(string name, ElementContentType type, int length) => (Name, Type, Length) = (name, type, length);

      public virtual string ToText(IDataModel rawData, int offset) {
         switch (Type) {
            case ElementContentType.PCS:
               return PCSString.Convert(rawData, offset, Length);
            case ElementContentType.Integer:
               return ToInteger(rawData, offset, Length).ToString();
            case ElementContentType.Pointer:
               var address = rawData.ReadPointer(offset);
               var anchor = rawData.GetAnchorFromAddress(-1, address);
               if (string.IsNullOrEmpty(anchor)) anchor = address.ToString("X6");
               return $"{PointerRun.PointerStart}{anchor}{PointerRun.PointerEnd}";
            default:
               throw new NotImplementedException();
         }
      }

      public static int ToInteger(IReadOnlyList<byte> data, int offset, int length) {
         int result = 0;
         int multiplier = 1;
         for (int i = 0; i < length; i++) {
            result += data[offset + i] * multiplier;
            multiplier *= 0x100;
         }
         return result;
      }
   }

   public class ArrayRunEnumSegment : ArrayRunElementSegment {
      public string EnumName { get; }

      public ArrayRunEnumSegment(string name, int length, string enumName) : base(name, ElementContentType.Integer, length) => EnumName = enumName;

      public override string ToText(IDataModel model, int offset) {
         var noChange = new NoDataChangeDeltaModel();
         using (ModelCacheScope.CreateScope(model)) {
            var options = GetOptions(model);
            if (options == null) return base.ToText(model, offset);

            var resultAsInteger = ToInteger(model, offset, Length);
            if (resultAsInteger >= options.Count) return base.ToText(model, offset);
            var value = options[resultAsInteger];

            // use ~2 postfix for a value if an earlier entry in the array has the same string
            var elementsUpToHereWithThisName = 1;
            for (int i = resultAsInteger - 1; i >= 0; i--) {
               var previousValue = options[i];
               if (previousValue == value) elementsUpToHereWithThisName++;
            }
            if (value.StartsWith("\"") && value.EndsWith("\"")) value = value.Substring(1, value.Length - 2);
            if (elementsUpToHereWithThisName > 1) value += "~" + elementsUpToHereWithThisName;

            // add quotes around it if it contains a space
            if (value.Contains(' ')) value = $"\"{value}\"";

            return value;
         }
      }

      public bool TryParse(IDataModel model, string text, out int value) {
         text = text.Trim();
         if (int.TryParse(text, out value)) return true;
         if (text.StartsWith("\"") && text.EndsWith("\"")) text = text.Substring(1, text.Length - 2);
         var partialMatches = new List<string>();
         var matches = new List<string>();
         if (!model.TryGetNameArray(EnumName, out var enumArray)) return false;

         // if the ~ character is used, expect that it's saying which match we want
         var desiredMatch = 1;
         var splitIndex = text.IndexOf('~');
         if (splitIndex != -1 && !int.TryParse(text.Substring(splitIndex + 1), out desiredMatch)) return false;
         if (splitIndex != -1) text = text.Substring(0, splitIndex);

         // ok, so everything lines up... check the array to see if any values match the string entered
         text = text.ToLower();
         for (int i = 0; i < enumArray.ElementCount; i++) {
            var option = PCSString.Convert(model, enumArray.Start + enumArray.ElementLength * i, enumArray.ElementContent[0].Length).ToLower();
            option = option.Substring(1, option.Length - 2);
            // check for exact matches
            if (option == text) {
               matches.Add(option);
               if (matches.Count == desiredMatch) {
                  value = i;
                  return true;
               }
            }
            // check for start-of-string matches (for autocomplete)
            if (option.StartsWith(text)) {
               partialMatches.Add(option);
               if (partialMatches.Count == desiredMatch && matches.Count == 0) {
                  value = i;
                  return true;
               }
            }
         }

         // we went through the whole array and didn't find it :(
         return false;
      }

      public IReadOnlyList<string> GetOptions(IDataModel model) {
         if (int.TryParse(EnumName, out var result)) return Enumerable.Range(0, result).Select(i => i.ToString()).ToList();
         return ModelCacheScope.GetCache(model).GetOptions(EnumName);
      }
   }

   public class ArrayRunBitArraySegment : ArrayRunElementSegment {
      public string SourceArrayName { get; }

      public ArrayRunBitArraySegment(string name, int length, string bitSourceName) : base(name, ElementContentType.BitArray, length) {
         SourceArrayName = bitSourceName;
      }

      public override string ToText(IDataModel rawData, int offset) {
         var result = new StringBuilder(Length * 3);
         for (int i = 0; i < Length; i++) {
            result.Append(rawData[offset + i].ToString("X2"));
            result.Append(" ");
         }
         return result.ToString();
      }

      public IReadOnlyList<string> GetOptions(IDataModel model) {
         var cache = ModelCacheScope.GetCache(model);
         return cache.GetBitOptions(SourceArrayName);
      }
   }

   /// <summary>
   /// For pointers that contain nested formatting instructions.
   /// For example, pointing to a text stream or a plm (pokemon learnable moves) stream
   /// </summary>
   public class ArrayRunPointerSegment : ArrayRunElementSegment {
      public string InnerFormat { get; }

      public bool IsInnerFormatValid {
         get {
            if (InnerFormat == PCSRun.SharedFormatString) return true;
            if (InnerFormat == PLMRun.SharedFormatString) return true;
            return false;
         }
      }

      public ArrayRunPointerSegment(string name, string innerFormat) : base(name, ElementContentType.Pointer, 4) {
         InnerFormat = innerFormat;
      }

      public bool DestinationDataMatchesPointerFormat(IDataModel owner, ModelDelta token, int destination) {
         if (destination == Pointer.NULL) return true;
         var run = owner.GetNextAnchor(destination);
         if (run.Start < destination) return false;
         if (run.Start > destination || (run.Start == destination && (run is NoInfoRun || run is PointerRun))) {
            // hard case: no format found, so check the data
            if (InnerFormat == PCSRun.SharedFormatString) {
               var length = PCSString.ReadString(owner, destination, true);

               if (length > 0) {
                  // our token will be a no-change token if we're in the middle of exploring the data.
                  // If so, don't actually add the run. It's enough to know that we _can_ add the run.
                  if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, new PCSRun(owner, destination, length));
                  return true;
               }
            } else if (InnerFormat == PLMRun.SharedFormatString) {
               var plmRun = new PLMRun(owner, destination);
               var length = plmRun.Length;
               if (length >= 2) {
                  if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, plmRun);
                  return true;
               }
            }
         } else {
            // easy case: already have a useful format, just see if it matches
            if (InnerFormat == PCSRun.SharedFormatString) return run is PCSRun;
            if (InnerFormat == PLMRun.SharedFormatString) return run is PLMRun;
         }
         return false;
      }
   }
}