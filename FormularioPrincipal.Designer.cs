namespace DlcDeHoi4;

partial class FormularioPrincipal
{
    private CheckBox casillaOscuro = null!;
    private CheckBox casillaSeleccionarTodos = null!;
    private TableLayoutPanel panelDeEstado = null!;
    private Button botonReintentar = null!;
    private ListaDeDlc listaDeDlc = null!;
    private Label etiquetaDeEstado = null!;
    private ListViewGroup grupoDeInstalados = null!;
    private ListViewGroup grupoDeIntegrados = null!;
    private TableLayoutPanel barraDeDescarga = null!;
    private CheckBox casillaSoundtracks = null!;
    private Button botonDescargar = null!;
    private Label etiquetaDescarga = null!;

    private void InitializeComponent()
    {
        var barraDeOpciones = new TableLayoutPanel();
        casillaOscuro = new CheckBox();
        casillaSeleccionarTodos = new CheckBox();
        panelDeEstado = new TableLayoutPanel();
        botonReintentar = new Button();
        listaDeDlc = new ListaDeDlc();
        etiquetaDeEstado = new Label();
        grupoDeInstalados = new ListViewGroup("instalados", "DLC instalados");
        grupoDeIntegrados = new ListViewGroup("integrados", "Incluidos en el juego base");
        barraDeDescarga = new TableLayoutPanel();
        casillaSoundtracks = new CheckBox();
        botonDescargar = new Button();
        etiquetaDescarga = new Label();
        SuspendLayout();
        barraDeOpciones.SuspendLayout();
        panelDeEstado.SuspendLayout();

        barraDeOpciones.AutoSize = true;
        barraDeOpciones.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        barraDeOpciones.ColumnCount = 2;
        barraDeOpciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barraDeOpciones.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barraDeOpciones.RowCount = 1;
        barraDeOpciones.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        barraDeOpciones.Dock = DockStyle.Top;
        barraDeOpciones.Padding = new Padding(10, 6, 10, 6);
        barraDeOpciones.TabIndex = 0;

        casillaSeleccionarTodos.AccessibleName = "Seleccionar todos los DLC disponibles";
        casillaSeleccionarTodos.Anchor = AnchorStyles.Left;
        casillaSeleccionarTodos.AutoCheck = false;
        casillaSeleccionarTodos.AutoSize = true;
        casillaSeleccionarTodos.Enabled = false;
        casillaSeleccionarTodos.Margin = Padding.Empty;
        casillaSeleccionarTodos.TabIndex = 0;
        casillaSeleccionarTodos.Text = "Seleccionar &todos";
        casillaSeleccionarTodos.ThreeState = true;
        casillaSeleccionarTodos.Visible = false;
        casillaSeleccionarTodos.Click += CambiarSeleccionDeTodos;
        barraDeOpciones.Controls.Add(casillaSeleccionarTodos, 0, 0);

        casillaOscuro.Anchor = AnchorStyles.Right;
        casillaOscuro.AutoSize = true;
        casillaOscuro.Margin = Padding.Empty;
        casillaOscuro.TabIndex = 1;
        casillaOscuro.Text = "Oscuro";
        casillaOscuro.CheckedChanged += CambiarTema;
        barraDeOpciones.Controls.Add(casillaOscuro, 1, 0);

        panelDeEstado.ColumnCount = 1;
        panelDeEstado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panelDeEstado.RowCount = 4;
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelDeEstado.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        panelDeEstado.Dock = DockStyle.Fill;
        panelDeEstado.Padding = new Padding(16);
        panelDeEstado.TabIndex = 2;

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
        listaDeDlc.TabIndex = 1;
        listaDeDlc.Visible = false;
        listaDeDlc.Columns.Add("DLC", 400);
        listaDeDlc.Columns.Add("ID de Steam", 95);
        listaDeDlc.Groups.AddRange([grupoDeInstalados, grupoDeIntegrados]);
        listaDeDlc.ItemCheck += ComprobarSeleccionDeDescarga;
        listaDeDlc.ItemChecked += CambiarSeleccionDeDescarga;

        barraDeDescarga.AutoSize = true;
        barraDeDescarga.Dock = DockStyle.Bottom;
        barraDeDescarga.Padding = new Padding(8);
        barraDeDescarga.TabIndex = 3;
        barraDeDescarga.ColumnCount = 3;
        barraDeDescarga.RowCount = 1;
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barraDeDescarga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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

        botonDescargar.Text = "Descargar e instalar";
        botonDescargar.AutoSize = true;
        botonDescargar.Enabled = false;
        botonDescargar.TabIndex = 1;
        botonDescargar.Click += DescargarSeleccionados;
        barraDeDescarga.Controls.Add(botonDescargar, 2, 0);

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
