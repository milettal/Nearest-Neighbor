using System.Windows;

namespace ClosestNeighborWpf {

	public partial class MainWindow : Window {

		public MainWindow() {
			DataContext = new MainWindowVM();
			InitializeComponent();
		}

	}

}
