﻿using System.Linq;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using P58_Loss.GlobalLib;

namespace P58_Loss.ElementProcess
{
    public sealed class PChiller : AMEPEquip
    {
        private sealed class ChillerRecognizer : AMEPRecognizer
        {
            public ChillerRecognizer(int dicSize = 1) : base(dicSize) { }
            public override bool Recognization(FamilyInstance fi)
            {
                _fi = fi;
                if (TryGetFIFloor(_doc)) return true;
                else return false;
            }
            public override void UpdateToPGs()
            {
                if(_addiInfo.defaultSet[(byte)DefaultSet.Chiller_Install] == 0
                    && _addiInfo.defaultSet[(byte)DefaultSet.Chiller_DamageMode] != 1)
                {
                    _abandonWriter.WriteAbandonment(_fi, AbandonmentTable.Chiller_InsDMConflict);
                    return;
                }
                else
                {
                    string FGCode = "D3031.01";
                    FGCode += (_addiInfo.defaultSet[(byte)DefaultSet.Chiller_Install] + 1).ToString();
                    int index_letter = _addiInfo.defaultSet[(byte)DefaultSet.Chiller_Install] == 0 ?
                        _addiInfo.defaultSet[(byte)DefaultSet.Chiller_Capacity] :
                        _addiInfo.defaultSet[(byte)DefaultSet.Chiller_Capacity] * 3 
                                     + _addiInfo.defaultSet[(byte)DefaultSet.Chiller_DamageMode];
                    FGCode += ConstSet.Alphabet[index_letter];

                    int index;
                    if (_dictionary.TryGetValue(FGCode, out index))
                    {
                        _PGItems.ElementAt(index).Num[_floor] += 1.0;
                    }
                    else
                    {
                        PGItem pgItem = new PGItem();
                        pgItem.PGName = "冷却器";
                        pgItem.PinYinSuffix = "LengQueQi";
                        pgItem.Code = FGCode;
                        pgItem.direction = Direction.Undefined;
                        pgItem.Num[_floor] += 1.0;
                        pgItem.Price = _addiInfo.prices[(byte)PGComponents.Chiller];
                        if (pgItem.Price == 0.0) pgItem.IfDefinePrice = false;
                        else pgItem.IfDefinePrice = true;
                        _PGItems.Add(pgItem);
                        _dictionary.Add(FGCode, _PGItems.Count - 1);
                    }
                }
            }
        }
        public PChiller(Document doc, AdditionalInfo addiInfo) : base(doc, addiInfo)
        {
            _PGItems = new List<PGItem>(1);
            _equips = new List<FamilyInstance>(20);
            _mepComp = MEPComponents.Chiller;
            _mepRecog = new ChillerRecognizer();
        }
    }
}
