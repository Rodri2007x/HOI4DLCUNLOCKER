namespace DlcDeHoi4;

partial class FormularioPrincipal
{
    private CheckBox casillaOscuro = null!;
    private TableLayoutPanel panelDeEstado = null!;
    private Button botonReintentar = null!;
    private ListaDeDlc listaDeDlc = null!;
    private Label etiquetaDeEstado = null!;
    private ListViewGroup grupoDeInstalados = null!;
    private ListViewGroup grupoDeIntegrados = null!;
    private TableLayoutPanel barraDeDescarga = null!;
    private CheckBox casillaSoundtracks = null!;
    private Button botonDescargar = null!;
    private Button botonCancelarDescarga = null!;
    private Label etiquetaDescarga = null!;
    private ProgressBar progresoDescarga = null!;

    private void InitializeComponent()
    {
        var barraDeOpciones = new FlowLayoutPanel();
        casillaOscuro = new CheckBox();
        panelDeEstado = new TableLayoutPanel();
        botonReintentar = new Button();
        listaDeDlc = new ListaDeDlc();
        etiquetaDeEstado = new Label();
        grupoDeInstalados = new ListViewGroup("instalados", "DLC instalados");
        grupoDeIntegrados = new ListViewGroup("integrados", "Incluidos en el juego base");
        barraDeDescarga = new TableLayoutPanel();
        casillaSoundtracks = new CheckBox();
        botonDescargar = new Button();
        botonCancelarDescarga = new Button();
        etiquetaDescarga = new Label();
        progresoDescarga = new ProgressBar();
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
        listaDeDlc.HideSelection = false;
        listaDeDlc.Visible = false;
        listaDeDlc.Columns.Add("DLC", 400);
        listaDeDlc.Columns.Add("ID de Steam", 95);
        listaDeDlc.Groups.AddRange([grupoDeInstalados, grupoDeIntegrados]);
        listaDeDlc.ItemCheck += ComprobarSeleccionDeDescarga;
        listaDeDlc.ItemChecked += CambiarSeleccionDeDescarga;

        barraDeDescarga.AutoSize = true;
        barraDeDescarga.Dock = DockStyle.Bottom;
        barraDeDescarga.Padding = new Padding(8);
        barraDeDescarga.ColumnCount = 4;
        barraDeDescarga.RowCount = 2;
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barraDeDescarga.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        barraDeDescarga.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        barraDeDescarga.Visible = false;

        etiquetaDescarga.AutoEllipsis = true;
        etiquetaDescarga.Dock = DockStyle.Fill;
        etiquetaDescarga.TextAlign = ContentAlignment.MiddleLeft;
        barraDeDescarga.Controls.Add(etiquetaDescarga, 0, 0);

        casillaSoundtracks.AccessibleName = "Incluir soundtracks";
        casillaSoundtracks.Anchor = AnchorStyles.Left;
        casillaSoundtracks.AutoSize = true;
        casillaSoundtracks.Margin = new Padding(8, 3, 8, 3);
        casillaSoundtracks.TabIndex = 0;
        casillaSoundtracks.Text = "Incluir &soundtracks";
        casillaSoundtracks.Visible = false;
        casillaSoundtracks.CheckedChanged += CambiarInclusionDeSoundtracks;
        barraDeDescarga.Controls.Add(casillaSoundtracks, 1, 0);

        botonCancelarDescarga.Text = "Cancelar";
        botonCancelarDescarga.AutoSize = true;
        botonCancelarDescarga.TabIndex = 1;
        botonCancelarDescarga.Visible = false;
        botonCancelarDescarga.Click += CancelarDescarga;
        barraDeDescarga.Controls.Add(botonCancelarDescarga, 2, 0);

        botonDescargar.Text = "Descargar";
        botonDescargar.AutoSize = true;
        botonDescargar.Enabled = false;
        botonDescargar.TabIndex = 2;
        botonDescargar.Click += DescargarSeleccionados;
        barraDeDescarga.Controls.Add(botonDescargar, 3, 0);

        progresoDescarga.AccessibleName = "Progreso de descarga";
        progresoDescarga.Dock = DockStyle.Fill;
        progresoDescarga.Height = 12;
        progresoDescarga.Visible = false;
        barraDeDescarga.Controls.Add(progresoDescarga, 0, 1);
        barraDeDescarga.SetColumnSpan(progresoDescarga, 4);

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Window;
        ClientSize = new Size(650, 420);
        MinimumSize = new Size(520, 300);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Unlocker";
        Controls.Add(listaDeDlc);
        Controls.Add(panelDeEstado);
        Controls.Add(barraDeDescarga);
        Controls.Add(barraDeOpciones);

        barraDeOpciones.ResumeLayout(false);
        barraDeOpciones.PerformLayout();
        panelDeEstado.ResumeLayout(false);
        panelDeEstado.PerformLayout();
        ResumeLayout(false);
    }
}
