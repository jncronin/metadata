using System;
using System.Collections.Generic;
using System.Text;

namespace metadata
{
    partial class MetadataStream
    {
        public string DebugGetDocumentNameFromBlob(int docb)
        {
            // load the document name
            var blob_len = (int)SigReadUSCompressed(ref docb);
            var start_docb = docb;
            var sep = sh_blob.di.ReadUTF8(ref docb);
            var part_idx = new List<string>();
            while (docb < (start_docb + blob_len))
            {
                var curidx = (int)SigReadUSCompressed(ref docb);
                if (curidx == 0)
                    part_idx.Add(string.Empty);
                else
                {
                    var blen = (int)SigReadUSCompressed(ref curidx);
                    var startidx = curidx;

                    StringBuilder sb = new StringBuilder();

                    while (curidx < (startidx + blen))
                    {
                        var c = sh_blob.di.ReadUTF8(ref curidx);
                        sb.Append(c);
                    }

                    part_idx.Add(sb.ToString());
                }

            }
            var doc_name = string.Join(sep.ToString(), part_idx);
            return doc_name;
        }

        public string DebugGetDocumentNameFromDocID(int id)
        {
            var docb = (int)GetIntEntry((int)metadata.MetadataStream.TableId.Document,
                (int)id, 0);

            return DebugGetDocumentNameFromBlob(docb);
        }

        public IList<SeqPt> DebugGetSeqPtForMDRow(int mdidx)
        {
            if (mdidx >= table_rows[(int)TableId.MethodDebugInformation])
                return null;

            var docb = (int)GetIntEntry((int)metadata.MetadataStream.TableId.MethodDebugInformation,
                mdidx, 0);
            //var docb = (int)GetIntEntry((int)metadata.MetadataStream.TableId.Document,
            //    (int)doc_id, 0);
            var spts = (int)GetIntEntry((int)metadata.MetadataStream.TableId.MethodDebugInformation,
                mdidx, 1);

            // Read sequence points
            var sps = new List<SeqPt>();

            var blob_len = (int)SigReadUSCompressed(ref spts);
            var start = spts;

            // header
            var locsig = (int)SigReadUSCompressed(ref spts);
            if (docb == 0)
                docb = (int)SigReadUSCompressed(ref spts);

            // read individual entries
            SeqPt prev = null;
            SeqPt prevnn = null;
            while (spts < (start + blob_len))
            {
                var diloffset = (int)SigReadUSCompressed(ref spts);
                if (diloffset == 0 && prev != null)
                {
                    // document-record
                    docb = (int)SigReadUSCompressed(ref spts);
                }
                else
                {
                    var iloffset = (prev == null) ? diloffset : prev.IlOffset + diloffset;
                    var dlines = (int)SigReadUSCompressed(ref spts);
                    var dcolumns = (dlines == 0) ? (int)SigReadUSCompressed(ref spts) :
                        SigReadSCompressed(ref spts);

                    var sp = new SeqPt(this);
                    sp.DocRowId = docb;
                    sp.IlOffset = iloffset;

                    if (dlines == 0 && dcolumns == 0)
                    {
                        // hidden-sequence-point-record
                        sp.StartLine = 0xfeefee;
                        sp.EndLine = 0xfeefee;
                        sp.StartCol = 0;
                        sp.EndCol = 0;
                        sps.Add(sp);
                        prev = sp;
                    }
                    else
                    {
                        // sequence-point-record
                        var dstartline = (prevnn == null) ? (int)SigReadUSCompressed(ref spts) :
                            SigReadSCompressed(ref spts);
                        var dstartcolumn = (prevnn == null) ? (int)SigReadUSCompressed(ref spts) :
                            SigReadSCompressed(ref spts);

                        var startline = (prevnn == null) ? dstartline : (dstartline + prevnn.StartLine);
                        var startcolumn = (prevnn == null) ? dstartcolumn : (dstartcolumn + prevnn.StartCol);

                        sp.StartLine = startline;
                        sp.EndLine = startline + dlines;
                        sp.StartCol = startcolumn;
                        sp.EndCol = startcolumn + dcolumns;
                        sps.Add(sp);
                        prev = sp;
                        prevnn = sp;
                    }
                }
            }

            return sps;
        }

        public class SeqPt
        {
            public int IlOffset { set; get; }
            public int StartLine { set; get; }
            public int EndLine { set; get; }
            public int StartCol { set; get; }
            public int EndCol { set; get; }
            public int DocRowId { set; get; }

            public string DocName
            {
                get
                {
                    return m.DebugGetDocumentNameFromDocID(DocRowId);
                }
            }

            MetadataStream m;
            internal SeqPt(MetadataStream mstream) { m = mstream; }

            public override string ToString()
            {
                if (IsHidden)
                {
                    return "IL" + IlOffset.ToString("X4") + " (hidden)";
                }
                return "IL" + IlOffset.ToString("X4") + " (" +
                    StartLine.ToString() + "-" + EndLine.ToString() + ") (" +
                    StartCol.ToString() + "-" + EndCol.ToString() + ")";
            }

            public bool IsHidden
            {
                get
                {
                    if (StartLine == 0xfeefee &&
                        EndLine == 0xfeefee &&
                        StartCol == 0 &&
                        EndCol == 0)
                        return true;
                    return false;
                }
            }
        }

    }
}
