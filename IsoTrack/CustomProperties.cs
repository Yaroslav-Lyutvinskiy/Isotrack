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

            public void OverrideProperty(PropertyDescriptor pd)
            {
                overridePds[pd.Name] = pd;
            }

            public override object GetPropertyOwner(PropertyDescriptor pd)
            {
                object o = base.GetPropertyOwner(pd);

                if (o == null)
                {
                    return this;
                }

                return o;
            }

            public PropertyDescriptorCollection GetPropertiesImpl(PropertyDescriptorCollection pdc)
            {
                List<PropertyDescriptor> pdl = new List<PropertyDescriptor>(pdc.Count+1);

                foreach (PropertyDescriptor pd in pdc)
                {
                    if (overridePds.ContainsKey(pd.Name))
                    {
                        pdl.Add(overridePds[pd.Name]);
                    }
                    else
                    {
                        pdl.Add(pd);
                    }
                }

                PropertyDescriptorCollection ret = new PropertyDescriptorCollection(pdl.ToArray());

                return ret;
            }

            public override PropertyDescriptorCollection GetProperties()
            {
                return GetPropertiesImpl(base.GetProperties());
            }
            public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
            {
                return GetPropertiesImpl(base.GetProperties(attributes));
            }
        }

    public class TypeDescriptorOverridingProvider : TypeDescriptionProvider
        {
            private readonly ICustomTypeDescriptor ctd;

            public TypeDescriptorOverridingProvider(ICustomTypeDescriptor ctd)
            {
                this.ctd = ctd;
            }

            public override ICustomTypeDescriptor GetTypeDescriptor (Type objectType, object instance)
            {
                return ctd;
            }
        }



    public class TaskConverter : StringConverter
    {
        static string[] TaskList = { "Targeted Analysis", "Untargeted Analysis", "Standards Refine", "Raw Data Caching" };

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

        public override System.ComponentModel.TypeConverter.StandardValuesCollection 
               GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(TaskList);
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
                        case "Standards_List":{
                            Dialog = Program.MainForm.OpenStandsFileDialog;
                            do{
                                if (Dialog.ShowDialog() != DialogResult.OK) break;
                            } while (!Program.MainForm.LoadStandFiles(Dialog.FileName));
                            value = Dialog.FileName;
                            return value;
                        }
                        case "Out_dbfile":{
                            Dialog = Program.MainForm.DB3SaveDialog;
                            break;
                        }
                        case "OutStandards":{
                            Dialog = Program.MainForm.TextSaveDialog;
                            Dialog.Title = "Specify text file to save outer standart list";
                            break;
                        }
                        case "StandardsReport":{
                            Dialog = Program.MainForm.TextSaveDialog;
                            Dialog.Title = "Specify text file to save report on standard search";
                            break;
                        }
                    }
                    if (Dialog.ShowDialog() == DialogResult.OK) 
                    {
                        value = Dialog.FileName;
                    }
                }
                return value; // can also replace the wrapper object here
            }
    }

    class TargetListInEditor: UITypeEditor
    {

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService svc = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            string TargetSource = value as string;
            if (svc != null )
            {
                MasterForms.LoadTargetListForm TargetDialog = new MasterForms.LoadTargetListForm();
                //parse existing string of format file|db3|mysql:FileName|(Method|FileSet:Name)
                TargetDialog.Setup(TargetSource);
                TargetDialog.FromGrid();
                if (svc.ShowDialog(TargetDialog) == DialogResult.OK) 
                {
                    value = TargetDialog.GetTargets();
                }
            }
            return value; // can also replace the wrapper object here
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
                FileListDialog.FromGrid();
                if (svc.ShowDialog(FileListDialog) == DialogResult.OK) 
                {
                    value = FileListDialog.GetProperty();
                }
            }
            return value; // can also replace the wrapper object here
        }
    }

    class AdductListEditor: UITypeEditor{
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }


        public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService svc = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            string Adducts = value as string;
            if (svc != null )
            {
                MasterForms.AdductsForm AdductDialog = new MasterForms.AdductsForm();
                //preparing form
                AdductDialog.Setup(Adducts);
                if (svc.ShowDialog(AdductDialog) == DialogResult.OK) 
                {
                    value = AdductDialog.GetAdducts();
                }
            }
            return value; // can also replace the wrapper object here
        }

    }


    public partial class Form1 : Form{
        ICustomTypeDescriptor InitialDesc;
        TypeDescriptorOverridingProvider ChangedDesc=null;

        private void ChangeProperties(){

            IsoTrack.Properties.Settings Settings = IsoTrack.Properties.Settings.Default;
            PropertyOverridingTypeDescriptor Desc = new PropertyOverridingTypeDescriptor(InitialDesc);
            PropertyDescriptor PD;
            PropertyDescriptor PDChanged;

        //Tab characters are used for ordering properties and categories
        //General
            //Task
            PD = TypeDescriptor.GetProperties(Settings)["Task"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\t\t\t\tGeneral" ),
                new DescriptionAttribute("Type of task in metabolomics workflow"),
                new DisplayNameAttribute("\t\t\tTask"),
                new TypeConverterAttribute(typeof(TaskConverter))
                );
            Desc.OverrideProperty(PDChanged);
            //processors
            PD = TypeDescriptor.GetProperties(Settings)["Processes"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\t\t\t\tGeneral" ),
                new DescriptionAttribute("Number of processes created in parallel for data processing"),
                new DisplayNameAttribute("\t\tProcesses")
                );
            Desc.OverrideProperty(PDChanged);
            //MySQLConnString
            PD = TypeDescriptor.GetProperties(Settings)["MySQLConnString"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD, 
                new CategoryAttribute( "\t\t\t\tGeneral" ),
                new DescriptionAttribute("Connection string to MySQL database (optional)")
                );
            Desc.OverrideProperty(PDChanged);

        //Inputs
            //FileList
            PD = TypeDescriptor.GetProperties(Settings)["FileList"];
            if (Settings.Task=="Standards Refine"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false));
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\t\t\t\tInput" ),
                    new DescriptionAttribute("Form Raw File List for Processing"),
                    new EditorAttribute(typeof(FileListEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tFile List")
                    );
            }
            Desc.OverrideProperty(PDChanged);

            //TargetList
            PD = TypeDescriptor.GetProperties(Settings)["TargetList"];
            if (Settings.Task=="Untargeted Analysis" || Settings.Task=="Raw Data Caching"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false));
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\t\t\t\tInput" ),
                    new DescriptionAttribute("Tab separated text file with standards info"),
                    new EditorAttribute(typeof(TargetListInEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tTargets List")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Standards List
            PD = TypeDescriptor.GetProperties(Settings)["Standards_List"];
            if (Settings.Task=="Standards Refine"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\t\t\t\tInput" ),
                    new DescriptionAttribute("Raw File-Standards Pair List Text File"),
                    new EditorAttribute(typeof(FileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tStandards List")
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false));
            }
            Desc.OverrideProperty(PDChanged);
            //IgnoreRT
            PD = TypeDescriptor.GetProperties(Settings)["IgnoreRT"];
            if (Settings.Task=="Standards Refine"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\t\t\t\tInput" ),
                    new DescriptionAttribute("Ignore RT times in Targets list"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("Ignore RT")
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false));
            }
            Desc.OverrideProperty(PDChanged);
            //Adducts
            PD = TypeDescriptor.GetProperties(Settings)["Adducts"];
            if (Settings.Task=="Standards Refine"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\t\t\t\tInput" ),
                    new DescriptionAttribute("List of ion adducts"),
                    new EditorAttribute(typeof(AdductListEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("Adducts")
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false));
            }
            Desc.OverrideProperty(PDChanged);


        //LC-MS Setup
            //Mass_Accuracy
            PD = TypeDescriptor.GetProperties(Settings)["Mass_Accuracy"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\t\tLC-MS setup"),
                    new DescriptionAttribute("Mass spectrometer Mass Accuracy in ppm"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\t\tMass Accuracy")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //RTError
            PD = TypeDescriptor.GetProperties(Settings)["RTError"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\t\tLC-MS setup"),
                    new DescriptionAttribute("Maximum expected shift in retention time for target metabolites"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tRTError")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //MinRTWidth
            PD = TypeDescriptor.GetProperties(Settings)["MinRTWidth"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\t\tLC-MS setup"),
                    new DescriptionAttribute("Minimum expected metabolite peak width in retention time dimension"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("Min RT Peak Width")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Profile
            PD = TypeDescriptor.GetProperties(Settings)["Profile"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\t\tLC-MS setup"),
                    new BrowsableAttribute(true),
                    new DescriptionAttribute("Use profile mass spectrometry data if available")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Gapped scans maximum
            PD = TypeDescriptor.GetProperties(Settings)["Gap_Scans_Max"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\t\tLC-MS setup"),
                    new BrowsableAttribute(true),
                    new DescriptionAttribute("Maximum number of consequent zero intensity scans to be included in trace"),
                    new DisplayNameAttribute("Gap Scan Max")
                    );
            }
            Desc.OverrideProperty(PDChanged);

        //Isotope check
            //C13_to_Check
            PD = TypeDescriptor.GetProperties(Settings)["C13_to_Check"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tIsotope check"),
                    new DescriptionAttribute("Number of C13 isotopes to check if other value is not described in target list"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tC13 to Check")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //N15_to_Check
            PD = TypeDescriptor.GetProperties(Settings)["N15_to_Check"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tIsotope check"),
                    new DescriptionAttribute("Number of N15 isotopes to check if other value is not described in target list"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tN15 to Check")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //C13Only
            PD = TypeDescriptor.GetProperties(Settings)["C13Only"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tIsotope check"),
                    new BrowsableAttribute(true),
                    new DescriptionAttribute("Check only C13 isotopes whatever is specified in target list ")
                    );
            }
            Desc.OverrideProperty(PDChanged);

        //Wavelet peak detection
            //PeakMaxWidth
            PD = TypeDescriptor.GetProperties(Settings)["PeakMaxWidth"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tWavelet peak detection"),
                    new DescriptionAttribute("Maximum RT peak width supposed to be detected by wavelet transform"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tPeak Max Width")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //PeakMinWidth
            PD = TypeDescriptor.GetProperties(Settings)["PeakMinWidth"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tWavelet peak detection"),
                    new DescriptionAttribute("Minimum RT peak width supposed to be detected by wavelet transform"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\tPeak Min Width")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //MinIntensity
            PD = TypeDescriptor.GetProperties(Settings)["MinIntensity"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tWavelet peak detection"),
                    new DescriptionAttribute("Minimum apex intensity for peaks"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("Min. Intensity")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //BaselineRatio
            PD = TypeDescriptor.GetProperties(Settings)["BaselineRatio"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("\t\tWavelet peak detection"),
                    new DescriptionAttribute("Minimum Signal/Noise ratio for peaks supposed to be detected by wavelet transform"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("S/N Ratio")
                    );
            }
            Desc.OverrideProperty(PDChanged);

        //Task Related Misc.
            //IntensityThreshold (for Untargeted and Standards)
            PD = TypeDescriptor.GetProperties(Settings)["IntensityThreshold"];
            if (Settings.Task == "Targeted Analysis" ){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Minimum Intensity to pick up trace from that point"),
                    new DisplayNameAttribute("Intensity Threshold"), 
                    new BrowsableAttribute(true)
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //SelectivityThreshold (for Standarts)
            PD = TypeDescriptor.GetProperties(Settings)["SelectivityThreshold"];
            if (Settings.Task == "Standards Refine"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Minimum selectivity score for standard trace to be selected as potential target"),
                    new DisplayNameAttribute("Selectivity Threshold"), 
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Commons
            PD = TypeDescriptor.GetProperties(Settings)["Commons"];
            if (Settings.Task == "Untargeted Analysis"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Minimum number of files where compaund has been detected to be recognized as a potential target"),
                    new DisplayNameAttribute("Common in files"), 
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //PeaksOnly
            PD = TypeDescriptor.GetProperties(Settings)["PeaksOnly"];
            if (Settings.Task == "Untargeted Analysis"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Select only targets which generate recognizeble peak at least once"),
                    new DisplayNameAttribute("Peaks Only"), 
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Back Filling
            PD = TypeDescriptor.GetProperties(Settings)["BackFilling"];
            if (Settings.Task == "Targeted Analysis"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Fill traces by second pass for maximim RT boundaries achieved in first pass"),
                    new DisplayNameAttribute("Back Filling"), 
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);

            //Arbitrary Main Trace 
            PD = TypeDescriptor.GetProperties(Settings)["ArbMainTrace"];
            if (Settings.Task == "Targeted Analysis"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Search for maximum intensity isotope signal to use it as main trace"),
                    new DisplayNameAttribute("Arb. Main Trace"), 
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Low signal targets
            PD = TypeDescriptor.GetProperties(Settings)["Low_signals"];
            if (Settings.Task == "Targeted Analysis"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "\tTask Related Misc." ),
                    new DescriptionAttribute("Get low signal, gapped traces from target area"),
                    new DisplayNameAttribute("Low Signals"), 
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);


        //Outputs
            //Out_dbfile
            PD = TypeDescriptor.GetProperties(Settings)["Out_dbfile"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("Outputs"),
                    new DescriptionAttribute("Name of db3 file to store all processing data "),
                    new BrowsableAttribute(true),
                    new EditorAttribute(typeof(FileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new DisplayNameAttribute("\t\tDB3 File")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //Save raw signals to db3 file
            PD = TypeDescriptor.GetProperties(Settings)["SaveProfile"];
            PDChanged = TypeDescriptor.CreateProperty(
                Settings.GetType(), PD,
                new CategoryAttribute("Outputs"),
                new DescriptionAttribute("Save raw profile signals to db3 file"),
                new BrowsableAttribute(true),
                new DisplayNameAttribute("\t\tSave Raw Profile")
                );
            Desc.OverrideProperty(PDChanged);
            //WriteTexts
            PD = TypeDescriptor.GetProperties(Settings)["WriteTexts"];
            if(Settings.Task == "Raw Data Caching") {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new BrowsableAttribute(false));
            } else {
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD,
                    new CategoryAttribute("Outputs"),
                    new DescriptionAttribute("Whether to write a text reports for each raw file"),
                    new BrowsableAttribute(true),
                    new DisplayNameAttribute("\t\tText Reports")
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //OutStandards (for Untargeted and Stands refine)
            PD = TypeDescriptor.GetProperties(Settings)["OutStandards"];
            if (Settings.Task == "Targeted Analysis" || Settings.Task == "Raw Data Caching"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "Outputs" ),
                    new DescriptionAttribute("Where to write resulting target list"),
                    new DisplayNameAttribute("\tTarget List - out"), 
                    new EditorAttribute(typeof(FileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new BrowsableAttribute(true)
                    );
            }
            Desc.OverrideProperty(PDChanged);
            //StandardsReport
            PD = TypeDescriptor.GetProperties(Settings)["StandardsReport"];
            if (Settings.Task == "Standards Refine"){
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new CategoryAttribute( "Outputs" ),
                    new DescriptionAttribute("File to store report on found standards"),
                    new DisplayNameAttribute("\tStandards Report"), 
                    new EditorAttribute(typeof(FileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                    new BrowsableAttribute(true)
                    );
            }else{
                PDChanged = TypeDescriptor.CreateProperty(
                    Settings.GetType(), PD, 
                    new BrowsableAttribute(false)
                    );
            }
            Desc.OverrideProperty(PDChanged);

            if (ChangedDesc!=null){
                TypeDescriptor.RemoveProvider(ChangedDesc,Settings);
            }
            ChangedDesc = new TypeDescriptorOverridingProvider(Desc);
            TypeDescriptor.AddProvider(ChangedDesc, Settings);           
        }
    }

}        