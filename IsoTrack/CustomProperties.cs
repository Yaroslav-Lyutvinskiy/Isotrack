using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;

namespace IsoTrack
{
    public class PropertyOverridingTypeDescriptor : CustomTypeDescriptor
        {
            private readonly Dictionary<string, PropertyDescriptor> overridePds = new Dictionary<string, PropertyDescriptor>();

            public PropertyOverridingTypeDescriptor(ICustomTypeDescriptor parent)
                : base(parent)
            { }

            public void OverrideProperty(PropertyDescriptor pd){
                overridePds[pd.Name] = pd;
            }

            public override object GetPropertyOwner(PropertyDescriptor pd){
                object o = base.GetPropertyOwner(pd);
                if (o == null){
                    return this;
                }
                return o;
            }

            public PropertyDescriptorCollection GetPropertiesImpl(PropertyDescriptorCollection pdc)
            {
                List<PropertyDescriptor> pdl = new List<PropertyDescriptor>(pdc.Count+1);

                foreach (PropertyDescriptor pd in pdc){
                    if (overridePds.ContainsKey(pd.Name)){
                        pdl.Add(overridePds[pd.Name]);
                    }else{
                        pdl.Add(pd);
                    }
                }
                PropertyDescriptorCollection ret = new PropertyDescriptorCollection(pdl.ToArray());
                return ret;
            }

            public override PropertyDescriptorCollection GetProperties(){
                return GetPropertiesImpl(base.GetProperties());
            }

            public override PropertyDescriptorCollection GetProperties(Attribute[] attributes){
                return GetPropertiesImpl(base.GetProperties(attributes));
            }
        }

    public class TypeDescriptorOverridingProvider : TypeDescriptionProvider
        {
            private readonly ICustomTypeDescriptor ctd;

            public TypeDescriptorOverridingProvider(ICustomTypeDescriptor ctd){
                this.ctd = ctd;
            }

            public override ICustomTypeDescriptor GetTypeDescriptor (Type objectType, object instance){
                return ctd;
            }
        }



    public class TaskConverter : StringConverter
    {

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            //true means show a combobox
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            //true will limit to list. false will show the list, 
            //but allow free-form entry
            return true;
        }

    }

        class FileEditor: UITypeEditor
        {

            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return UITypeEditorEditStyle.Modal;
            }

            public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
            {
                IWindowsFormsEditorService svc = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
                if (svc != null)
                {
                    FileDialog Dialog = null;
                    switch (context.PropertyDescriptor.Name){
                        case "Out_dbfile":{
                            Dialog = Program.MainForm.DB3SaveDialog;
                            break;
                        }
                        case "OutTargets":{
                            Dialog = Program.MainForm.TextSaveDialog;
                            Dialog.Title = "Specify text file to save outer target list";
                            break;
                        }
                    }
                    if (Dialog.ShowDialog() == DialogResult.OK) 
                    {
                        value = Dialog.FileName;
                    }
                }
                return value; 
            }
    }


    class FileListEditor: UITypeEditor
    {

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService svc = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (ImportForm.ImportFile != "" && ImportForm.Pairing){
                DialogResult Quest = MessageBox.Show(
                    "Edidting file list after import from .db3 will \n"+
                    "destroy pairing, sample names and colors. \n"+
                    "Are you sure you wish to continue?", "File list editing", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                if (Quest != DialogResult.OK) return value;
                ImportForm.Pairing = false;
            }
            string TargetSource = value as string;
            if (svc != null )
            {
                MasterForms.LoadFileList FileListDialog = new MasterForms.LoadFileList ();
                FileListDialog.Setup();
                if (svc.ShowDialog(FileListDialog) == DialogResult.OK) 
                {
                    value = FileListDialog.GetProperty();
                }
            }
            return value; // can also replace the wrapper object here
        }
    }


    public partial class Form1 : Form{
        ICustomTypeDescriptor InitialDesc;
        TypeDescriptorOverridingProvider ChangedDesc=null;

        //Format properties to provide correct display and property editors
        private void ChangeProperties(){
            IsoTrack.Properties.Settings Settings = IsoTrack.Properties.Settings.Default;
            PropertyOverridingTypeDescriptor Desc = new PropertyOverridingTypeDescriptor(InitialDesc);
            PropertyDescriptor PD;
            PropertyDescriptor PDChanged;

        //Tab characters are used for ordering properties and categories
        //with more tabs properties are getting up in alphabetical order and, so, in order of object inspector 
        //Tabs by itself is not shown in object inspector
        //General
            //processors
            PD = TypeDescriptor.GetProperties(Settings)["Processes"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\t\t\t\tGeneral" ),
                new DescriptionAttribute("Number of processes created in parallel for data processing"),
                new DisplayNameAttribute("\t\tProcesses")
                );
            Desc.OverrideProperty(PDChanged);
        //Inputs
            //FileList
            PD = TypeDescriptor.GetProperties(Settings)["FileList"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\t\t\t\tInput" ),
                new DescriptionAttribute("Form Raw File List for Processing"),
                new EditorAttribute(typeof(FileListEditor), typeof(System.Drawing.Design.UITypeEditor)),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("\tFile List")
                );
            Desc.OverrideProperty(PDChanged);
        //LC-MS Setup
            //Mass_Accuracy
            PD = TypeDescriptor.GetProperties(Settings)["Mass_Accuracy"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\t\tLC-MS setup"),
                new DescriptionAttribute("Mass spectrometer Mass Accuracy in ppm"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("\t\t\tMass Accuracy")
                );
            Desc.OverrideProperty(PDChanged);
            //RTError
            PD = TypeDescriptor.GetProperties(Settings)["RTError"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\t\tLC-MS setup"),
                new DescriptionAttribute("Maximum expected shift in retention time for target metabolites"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("\t\tRTError")
                );
            Desc.OverrideProperty(PDChanged);
            //PeakMaxWidth
            PD = TypeDescriptor.GetProperties(Settings)["MaxRTWidth"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\t\tLC-MS setup"),
                new DescriptionAttribute("Maximum expected metabolite peak width in retention time dimension"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("Max RT Peak Width")
                );
            Desc.OverrideProperty(PDChanged);
            //MinRTWidth
            PD = TypeDescriptor.GetProperties(Settings)["MinRTWidth"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\t\tLC-MS setup"),
                new DescriptionAttribute("Minimum expected metabolite peak width in retention time dimension"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("\tMin RT Peak Width")
                );
            Desc.OverrideProperty(PDChanged);
            //Gapped scans maximum
            PD = TypeDescriptor.GetProperties(Settings)["Gap_Scans_Max"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\t\tLC-MS setup"),
                new BrowsableAttribute(true),
                new DescriptionAttribute("Maximum number of consequent zero intensity scans to be included in trace"),
                new DisplayNameAttribute("\tGap Scan Max")
                );
            Desc.OverrideProperty(PDChanged);

        //Isotope check
            //C13_to_Check
            PD = TypeDescriptor.GetProperties(Settings)["C13_to_Check"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\tIsotope check"),
                new DescriptionAttribute("Number of C13 isotopes to check if other value is not described in target list"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("\tC13 to Check")
                );
            Desc.OverrideProperty(PDChanged);

        //Wavelet peak detection
            //MinIntensity
            PD = TypeDescriptor.GetProperties(Settings)["MinIntensity"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\tWavelet peak detection"),
                new DescriptionAttribute("Minimum apex intensity for peaks"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("Min. Intensity")
                );
            Desc.OverrideProperty(PDChanged);
            //BaselineRatio
            PD = TypeDescriptor.GetProperties(Settings)["BaselineRatio"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("\t\tWavelet peak detection"),
                new DescriptionAttribute("Minimum Signal/Noise ratio for peaks supposed to be detected by wavelet transform"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("S/N Ratio")
                );
            Desc.OverrideProperty(PDChanged);

        //Task Related Misc.
            //IntensityThreshold (for Untargeted and Standards)
            PD = TypeDescriptor.GetProperties(Settings)["IntensityThreshold"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\tMisc." ),
                new DescriptionAttribute("Minimum Intensity to pick up trace from that point"),
                new DisplayNameAttribute("Intensity Threshold"), 
                new BrowsableAttribute(true)
                );
            Desc.OverrideProperty(PDChanged);
            //Commons
            PD = TypeDescriptor.GetProperties(Settings)["Commons"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\tMisc." ),
                new DescriptionAttribute("Minimum number of files where compaund has been detected to be recognized as a potential target"),
                new DisplayNameAttribute("Common in files"), 
                new BrowsableAttribute(true)
                );
            Desc.OverrideProperty(PDChanged);
        //Outputs
            //Out_dbfile
            PD = TypeDescriptor.GetProperties(Settings)["Out_dbfile"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("Outputs"),
                new DescriptionAttribute("Name of db3 file to store all processing data "),
                new BrowsableAttribute(true),
                new EditorAttribute(typeof(FileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                new DisplayNameAttribute("\t\tDB3 File")
                );
            Desc.OverrideProperty(PDChanged);

            //Text Target File (Should be compatible with Inspector)
            PD = TypeDescriptor.GetProperties(Settings)["OutTargets"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "Outputs" ),
                new DescriptionAttribute("Where to write resulting target list"),
                new DisplayNameAttribute("\tTarget List - out"), 
                new EditorAttribute(typeof(FileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                new BrowsableAttribute(true)
                );
            Desc.OverrideProperty(PDChanged);

            if (ChangedDesc!=null){
                TypeDescriptor.RemoveProvider(ChangedDesc,Settings);
            }
            ChangedDesc = new TypeDescriptorOverridingProvider(Desc);
            TypeDescriptor.AddProvider(ChangedDesc, Settings);           
        }
    }

}        