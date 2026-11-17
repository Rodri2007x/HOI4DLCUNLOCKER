namespace DlcDeHoi4;

partial class FormularioPrincipal
{
    private CheckBox casillaOscuro = null!;
    private TableLayoutPanel panelDeEstado = null!;
    private Button botonReintentar = null!;
    private ListView listaDeDlc = null!;
    private Label etiquetaDeEstado = null!;
    private ListViewGroup grupoDeInstalados = null!;
    private ListViewGroup grupoDeIntegrados = null!;

    private void InitializeComponent()
    {
        var barraDeOpciones = new FlowLayoutPanel();
        casillaOscuro = new CheckBox();
        panelDeEstado = new TableLayoutPanel();
        botonReintentar = new Button();
        listaDeDlc = new ListView();
        etiquetaDeEstado = new Label();
        grupoDeInstalados = new ListViewGroup("instalados", "DLC instalados");
        grupoDeIntegrados = new ListViewGroup("integrados", "Incluidos en el juego base");
        SuspendLayout();
        barraDeOpciones.SuspendLayout();
        panelDeEstado.SuspendLayout();

        barraDeOpciones.AutoSize = true;
        barraDeOpciones.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        barraDeOpciones.Dock = DockStyle.Top;
        barraDeOpciones.FlowDirection = FlowDirection.RightToLeft;
        barraDeOpciones.Padding = new Padding(10, 6, 10, 6);
        barraDeOpciones.WrapContents = false;

        casillaOscuro.AutoSize = true;
        casillaOscuro.Margin = Padding.Empty;
        casillaOscuro.Text = "Oscuro";
        casillaOscuro.CheckedChanged += CambiarTema;
        barraDeOpciones.Controls.Add(casillaOscuro);

        panelDeEstado.ColumnCount = 1;
        panelDeEstado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panelDeEstado.RowCount = 4;
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        panelDeEstado.Dock = DockStyle.Fill;
        panelDeEstado.Padding = new Padding(16);

        etiquetaDeEstado.AutoSize = true;
        etiquetaDeEstado.Anchor = AnchorStyles.None;
        etiquetaDeEstado.MaximumSize = new Size(440, 0);
        etiquetaDeEstado.Text = "Verificando instalación…";
        etiquetaDeEstado.TextAlign = ContentAlignment.MiddleCenter;
        panelDeEstado.Controls.Add(etiquetaDeEstado, 0, 1);

        botonReintentar.AutoSize = true;
        botonReintentar.Anchor = AnchorStyles.None;
        botonReintentar.Margin = new Padding(0, 8, 0, 0);
        botonReintentar.Text = "Reintentar";
        botonReintentar.Visible = false;
        botonReintentar.Click += ReintentarDeteccion;
        panelDeEstado.Controls.Add(botonReintentar, 0, 2);

        listaDeDlc.AccessibleName = "DLC detectados";
        listaDeDlc.Dock = DockStyle.Fill;
        listaDeDlc.View = View.Details;
        listaDeDlc.FullRowSelect = true;
        listaDeDlc.Visible = false;
        listaDeDlc.Columns.Add("DLC", 400);
        listaDeDlc.Columns.Add("App ID", 95);
        listaDeDlc.Groups.AddRange([grupoDeInstalados, grupoDeIntegrados]);

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Window;
        ClientSize = new Size(650, 420);
        MinimumSize = new Size(520, 300);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Unlocker";
        Controls.Add(listaDeDlc);
        Controls.Add(panelDeEstado);
        Controls.Add(barraDeOpciones);

        barraDeOpciones.ResumeLayout(false);
        barraDeOpciones.PerformLayout();
        panelDeEstado.ResumeLayout(false);
        panelDeEstado.PerformLayout();
        ResumeLayout(false);
    }
}
