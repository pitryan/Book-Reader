using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfiumViewer.Core;
using PdfiumViewer;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Book_Reader
{
    public partial class MainWindow : Window
    {
        private PdfDocument? _currentDocument;
        private readonly RecentFilesManager _recentManager;
        private readonly LibraryManager _libraryManager;
        private readonly BookMetadataManager _metadataManager;
        private readonly AppSettingsManager _appSettingsManager;

        private bool _isTocOpen = false;
        private int _currentPage = 0;
        private int _pageCount = 0;
        private int _currentSortIndex = 0;
        private double _zoomLevel = 1.0;
        private bool _isFitWidth = false;
        private bool _isDoublePage = false;
        private const double ZoomStep = 0.25;

        private string _fileToRename = "";

        public MainWindow()
        {
            InitializeComponent();
            _recentManager = new RecentFilesManager();
            _libraryManager = new LibraryManager();
            _metadataManager = new BookMetadataManager();
            _appSettingsManager = new AppSettingsManager();

            ApplyTheme(_appSettingsManager.Current.Theme);
        }

        private void ApplyTheme(string themeName)
        {
            try
            {
                var uri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);
                var resourceDict = Application.LoadComponent(uri) as ResourceDictionary;
                
                if (resourceDict != null)
                {
                    Application.Current.Resources.MergedDictionaries.Clear();
                    Application.Current.Resources.MergedDictionaries.Add(resourceDict);
                }
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (BtnThemeDropdown != null)
                BtnThemeDropdown.Content = _appSettingsManager.Current.Theme == "Light" ? "Light Mode ⏷" : "Dark Mode ⏷";
                
            ShowHome();
        }

        // ===========================================================
        //  VIEW SWITCHING & HOME SCREEN (SIDEBAR)
        // ===========================================================

        private void ShowHome()
        {
            HomeContainer.Visibility = Visibility.Visible;
            ReaderContainer.Visibility = Visibility.Collapsed;
            
            if (MenuRecent.IsChecked == true) LoadRecentFilesUI();
            else if (MenuLibrary.IsChecked == true) LoadLibraryUI();
            else if (MenuFolders.IsChecked == true) LoadFoldersUI();
            
            TxtStatus.Text = "Rak Buku";
        }

        private void ShowReader()
        {
            HomeContainer.Visibility = Visibility.Collapsed;
            ReaderContainer.Visibility = Visibility.Visible;
            PdfScrollViewer.Focus();
        }

        private void MenuHome_Checked(object sender, RoutedEventArgs e)
        {
            if (RecentView == null) return; 

            RecentView.Visibility = Visibility.Collapsed;
            LibraryView.Visibility = Visibility.Collapsed;
            FoldersView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            BtnAddFolder.Visibility = Visibility.Collapsed;
            BtnScanNow.Visibility = Visibility.Collapsed;

            if (MenuRecent.IsChecked == true)
            {
                TxtHomeTitle.Text = "Terakhir Dibaca";
                RecentView.Visibility = Visibility.Visible;
                LoadRecentFilesUI();
            }
            else if (MenuLibrary.IsChecked == true)
            {
                TxtHomeTitle.Text = "Semua Buku";
                LibraryView.Visibility = Visibility.Visible;
                BtnScanNow.Visibility = Visibility.Visible;
                LoadLibraryUI();
            }
            else if (MenuFolders.IsChecked == true)
            {
                TxtHomeTitle.Text = "Kelola Folder";
                FoldersView.Visibility = Visibility.Visible;
                BtnAddFolder.Visibility = Visibility.Visible;
                LoadFoldersUI();
            }
            else if (MenuSettings.IsChecked == true)
            {
                TxtHomeTitle.Text = "Pengaturan";
                SettingsView.Visibility = Visibility.Visible;
            }
        }

        private void LoadRecentFilesUI()
        {
            var files = _recentManager.GetRecentFiles();
            foreach(var f in files) f.IsFavorite = _metadataManager.IsFavorite(f.FilePath);

            if (files.Count > 0)
            {
                RecentFilesList.ItemsSource = files;
                RecentFilesList.Visibility = Visibility.Visible;
                TxtNoRecentFiles.Visibility = Visibility.Collapsed;
            }
            else
            {
                RecentFilesList.ItemsSource = null;
                RecentFilesList.Visibility = Visibility.Collapsed;
                TxtNoRecentFiles.Visibility = Visibility.Visible;
            }
        }

        private async void LoadLibraryUI()
        {
            if (TxtNoLibraryFiles == null) return;

            TxtNoLibraryFiles.Text = "Sedang memindai buku...";
            TxtNoLibraryFiles.Visibility = Visibility.Visible;
            LibraryFilesList.Visibility = Visibility.Collapsed;

            var books = await Task.Run(() => _libraryManager.ScanAllBooks());

            if (books.Count > 0)
            {
                foreach (var book in books)
                {
                    book.ThumbnailPath = GetOrGenerateThumbnailFast(book.FilePath);
                    book.IsFavorite = _metadataManager.IsFavorite(book.FilePath);
                }

                // Apply Sorting
                if (_currentSortIndex == 0) books = books.OrderBy(b => b.FileName).ToList();
                else if (_currentSortIndex == 1) books = books.OrderByDescending(b => b.FileName).ToList();
                else if (_currentSortIndex == 2) books = books.OrderByDescending(b => b.LastOpened).ToList();
                else if (_currentSortIndex == 3) books = books.Where(b => b.IsFavorite).ToList();

                LibraryFilesList.ItemsSource = books;
                LibraryFilesList.Visibility = Visibility.Visible;
                TxtNoLibraryFiles.Visibility = books.Count == 0 && _currentSortIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
                if (_currentSortIndex == 3 && books.Count == 0) TxtNoLibraryFiles.Text = "Belum ada buku favorit.";
            }
            else
            {
                LibraryFilesList.ItemsSource = null;
                LibraryFilesList.Visibility = Visibility.Collapsed;
                TxtNoLibraryFiles.Text = "Belum ada buku di perpustakaan. Tambahkan folder koleksimu di Kelola Folder!";
                TxtNoLibraryFiles.Visibility = Visibility.Visible;
            }
        }

        private void LoadFoldersUI()
        {
            FoldersList.ItemsSource = null;
            FoldersList.ItemsSource = _libraryManager.WatchedFolders;
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Pilih Folder Koleksi PDF" };
            if (dialog.ShowDialog() == true)
            {
                _libraryManager.AddFolder(dialog.FolderName);
                LoadFoldersUI();
                MenuLibrary.IsChecked = true;
            }
        }

        private void BtnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string folderPath)
            {
                _libraryManager.RemoveFolder(folderPath);
                LoadFoldersUI();
            }
        }

        private void BtnScanNow_Click(object sender, RoutedEventArgs e) => LoadLibraryUI();

        // ===========================================================
        //  LIBRARY VIEW TOGGLES & SORTING
        // ===========================================================

        private void BtnSortDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (BtnSortDropdown.ContextMenu != null)
            {
                BtnSortDropdown.ContextMenu.PlacementTarget = BtnSortDropdown;
                BtnSortDropdown.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                BtnSortDropdown.ContextMenu.IsOpen = true;
            }
        }

        private void SortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string tag && int.TryParse(tag, out int index))
            {
                _currentSortIndex = index;
                BtnSortDropdown.Content = $"Urutkan: {item.Header} ⏷";
                LoadLibraryUI();
            }
        }

        private void BtnViewMode_Checked(object sender, RoutedEventArgs e)
        {
            if (LibraryFilesList == null) return;

            if (BtnViewGrid.IsChecked == true)
            {
                LibraryFilesList.ItemTemplate = (DataTemplate)FindResource("BookCardDataTemplate");
                LibraryFilesList.ItemsPanel = (ItemsPanelTemplate)FindResource("WrapPanelTemplate");
                RecentFilesList.ItemTemplate = (DataTemplate)FindResource("BookCardDataTemplate");
                RecentFilesList.ItemsPanel = (ItemsPanelTemplate)FindResource("WrapPanelTemplate");
            }
            else
            {
                LibraryFilesList.ItemTemplate = (DataTemplate)FindResource("BookListDataTemplate");
                LibraryFilesList.ItemsPanel = (ItemsPanelTemplate)FindResource("StackPanelTemplate");
                RecentFilesList.ItemTemplate = (DataTemplate)FindResource("BookListDataTemplate");
                RecentFilesList.ItemsPanel = (ItemsPanelTemplate)FindResource("StackPanelTemplate");
            }
        }

        private void BtnThemeDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (BtnThemeDropdown.ContextMenu != null)
            {
                BtnThemeDropdown.ContextMenu.PlacementTarget = BtnThemeDropdown;
                BtnThemeDropdown.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                BtnThemeDropdown.ContextMenu.IsOpen = true;
            }
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string newTheme)
            {
                BtnThemeDropdown.Content = $"{item.Header} ⏷";
                if (_appSettingsManager.Current.Theme != newTheme)
                {
                    _appSettingsManager.Current.Theme = newTheme;
                    _appSettingsManager.SaveSettings();
                    ApplyTheme(newTheme);
                }
            }
        }

        // ===========================================================
        //  CONTEXT MENU ACTIONS
        // ===========================================================

        private string GetFilePathFromMenuItem(object sender)
        {
            if (sender is MenuItem item && item.Tag is string path) return path;
            return "";
        }

        private void MenuBuka_Click(object sender, RoutedEventArgs e)
        {
            string path = GetFilePathFromMenuItem(sender);
            if (!string.IsNullOrEmpty(path)) LoadPdfDocument(path);
        }

        private void MenuFavorit_Click(object sender, RoutedEventArgs e)
        {
            string path = GetFilePathFromMenuItem(sender);
            if (!string.IsNullOrEmpty(path))
            {
                bool isFav = _metadataManager.IsFavorite(path);
                _metadataManager.SetFavorite(path, !isFav);
                if (MenuLibrary.IsChecked == true) LoadLibraryUI();
                else if (MenuRecent.IsChecked == true) LoadRecentFilesUI();
            }
        }

        private void MenuRename_Click(object sender, RoutedEventArgs e)
        {
            string path = GetFilePathFromMenuItem(sender);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _fileToRename = path;
                TxtRenameInput.Text = Path.GetFileNameWithoutExtension(path);
                RenameOverlay.Visibility = Visibility.Visible;
                TxtRenameInput.Focus();
                TxtRenameInput.SelectAll();
            }
        }

        private void MenuHapus_Click(object sender, RoutedEventArgs e)
        {
            string path = GetFilePathFromMenuItem(sender);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var result = MessageBox.Show($"Apakah kamu yakin ingin menghapus file fisik ini?\n\n{path}", "Hapus File PDF", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(path);
                        _metadataManager.DeleteFile(path);
                        if (MenuLibrary.IsChecked == true) LoadLibraryUI();
                        else if (MenuRecent.IsChecked == true) LoadRecentFilesUI();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gagal menghapus file. Mungkin file sedang digunakan.\n\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ===========================================================
        //  RENAME OVERLAY ACTIONS
        // ===========================================================

        private void BtnRenameCancel_Click(object sender, RoutedEventArgs e)
        {
            RenameOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnRenameSave_Click(object sender, RoutedEventArgs e)
        {
            string newName = TxtRenameInput.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Nama file tidak boleh kosong.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(_fileToRename)!;
                string newPath = Path.Combine(dir, newName + ".pdf");

                if (File.Exists(newPath) && !_fileToRename.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("File dengan nama tersebut sudah ada.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                File.Move(_fileToRename, newPath);
                _metadataManager.RenameFile(_fileToRename, newPath);

                RenameOverlay.Visibility = Visibility.Collapsed;
                
                if (MenuLibrary.IsChecked == true) LoadLibraryUI();
                else if (MenuRecent.IsChecked == true) LoadRecentFilesUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengubah nama file. Mungkin file sedang dibuka.\n\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================================================
        //  FILE OPERATIONS & PDF RENDERING
        // ===========================================================

        private void RecentFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filePath)
            {
                if (File.Exists(filePath)) LoadPdfDocument(filePath);
                else
                {
                    MessageBox.Show("File tidak ditemukan. Mungkin sudah dihapus atau dipindah.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (MenuLibrary.IsChecked == true) LoadLibraryUI();
                    else LoadRecentFilesUI();
                }
            }
        }

        private void BtnCloseBook_Click(object sender, RoutedEventArgs e)
        {
            CloseDocument();
            ShowHome();
        }

        private void BtnBukaFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", Title = "Pilih File PDF" };
            if (dialog.ShowDialog() == true) LoadPdfDocument(dialog.FileName);
        }

        private void LoadPdfDocument(string filePath)
        {
            try
            {
                CloseDocument(); 
                _currentDocument = PdfDocument.Load(filePath);
                
                int savedPage = _metadataManager.GetLastReadPage(filePath);
                _currentPage = savedPage > 0 && savedPage < _currentDocument.PageCount ? savedPage : 0;
                _pageCount = _currentDocument.PageCount;
                _zoomLevel = 1.0; _isFitWidth = false;

                string thumbnailPath = GetOrGenerateThumbnailFast(filePath, _currentDocument);
                _recentManager.AddOrUpdateFile(filePath, thumbnailPath);

                ShowReader(); EnableControls(true); LoadTableOfContents();
                TxtStatus.Text = $"📄 {Path.GetFileName(filePath)}";

                RenderCurrentPages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka file PDF:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetOrGenerateThumbnailFast(string filePath, PdfDocument? doc = null)
        {
            try
            {
                string fileHash = string.Join("", System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(filePath)).Select(b => b.ToString("X2")));
                string thumbPath = Path.Combine(_recentManager.ThumbnailsFolder, $"{fileHash}.png");

                if (File.Exists(thumbPath)) return thumbPath;

                bool needsDispose = false;
                if (doc == null) { doc = PdfDocument.Load(filePath); needsDispose = true; }

                if (doc.PageCount > 0)
                {
                    var page = doc.Pages[0];
                    double scale = Math.Min(300.0 / page.Size.Width, 300.0 / page.Size.Height);
                    int w = (int)(page.Size.Width * scale); int h = (int)(page.Size.Height * scale);

                    using var image = page.Render(w, h, 96, 96, PdfRotation.Rotate0, PdfiumViewer.Enums.PdfRenderFlags.None);
                    image.Save(thumbPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                if (needsDispose) doc.Dispose();
                return thumbPath;
            }
            catch { return ""; }
        }

        private void CloseDocument()
        {
            _currentDocument?.Dispose(); _currentDocument = null;
            ImgLeftPage.Source = null; ImgRightPage.Source = null;
            BorderLeftPage.Visibility = Visibility.Collapsed; BorderRightPage.Visibility = Visibility.Collapsed;
            EnableControls(false); TocTreeView.ItemsSource = null;
            if (_isTocOpen) CloseTocSidebar();
        }

        private void RenderCurrentPages()
        {
            if (_currentDocument == null || _currentPage < 0 || _currentPage >= _pageCount) return;

            string? currentFile = _recentManager.GetRecentFiles().FirstOrDefault()?.FilePath;
            if (currentFile != null) _metadataManager.SetLastReadPage(currentFile, _currentPage);

            ImgLeftPage.Source = RenderPageToBitmap(_currentPage);
            BorderLeftPage.Visibility = Visibility.Visible;

            if (_isDoublePage && _currentPage + 1 < _pageCount)
            {
                BorderRightPage.Visibility = Visibility.Visible;
                ImgRightPage.Source = RenderPageToBitmap(_currentPage + 1);
            }
            else
            {
                BorderRightPage.Visibility = Visibility.Collapsed;
                ImgRightPage.Source = null;
            }

            UpdatePageInfo(); ApplyZoom();
            PdfScrollViewer.ScrollToVerticalOffset(0); PdfScrollViewer.ScrollToHorizontalOffset(0);
        }

        private BitmapImage? RenderPageToBitmap(int pageIndex)
        {
            try
            {
                var page = _currentDocument!.Pages[pageIndex];
                var size = page.Size;
                using var image = page.Render((int)size.Width, (int)size.Height, 96, 96, PdfRotation.Rotate0, PdfiumViewer.Enums.PdfRenderFlags.None);
                using var memory = new MemoryStream();
                image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit(); bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memory; bitmapImage.EndInit(); bitmapImage.Freeze();
                return bitmapImage;
            }
            catch { return null; }
        }

        // ===========================================================
        //  NAVIGATION, ZOOM, VIEW MODE
        // ===========================================================

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) => GoToPreviousPage();
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) => GoToNextPage();
        private void GoToPreviousPage() { if (_currentDocument == null) return; int step = _isDoublePage ? 2 : 1; if (_currentPage - step >= 0) { _currentPage -= step; RenderCurrentPages(); } else if (_currentPage > 0) { _currentPage = 0; RenderCurrentPages(); } }
        private void GoToNextPage() { if (_currentDocument == null) return; int step = _isDoublePage ? 2 : 1; if (_currentPage + step < _pageCount) { _currentPage += step; RenderCurrentPages(); } }
        private void GoToPage(int pageIndex) { if (_currentDocument == null) return; if (pageIndex >= 0 && pageIndex < _pageCount) { if (_isDoublePage && pageIndex % 2 != 0) pageIndex--; _currentPage = pageIndex; RenderCurrentPages(); } }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOut();
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => ZoomIn();
        private void BtnFitWidth_Click(object sender, RoutedEventArgs e) { if (_currentDocument == null) return; _isFitWidth = !_isFitWidth; ApplyZoom(); }
        private void ZoomIn() { if (_currentDocument == null) return; _isFitWidth = false; _zoomLevel = Math.Min(_zoomLevel + ZoomStep, 4.0); ApplyZoom(); }
        private void ZoomOut() { if (_currentDocument == null) return; _isFitWidth = false; _zoomLevel = Math.Max(_zoomLevel - ZoomStep, 0.25); ApplyZoom(); }
        private void ApplyZoom()
        {
            if (_currentDocument == null) return;
            if (_isFitWidth && PdfScrollViewer.ViewportWidth > 0)
            {
                double widthLeft = ImgLeftPage.Source?.Width ?? 600;
                double widthRight = _isDoublePage ? (ImgRightPage.Source?.Width ?? 600) : 0;
                double totalWidth = widthLeft + widthRight + (_isDoublePage ? 40 : 20);
                if (totalWidth > 0) { double scale = PdfScrollViewer.ViewportWidth / totalWidth; scale -= 0.02; _zoomLevel = scale; PdfScaleTransform.ScaleX = scale; PdfScaleTransform.ScaleY = scale; }
            }
            else { PdfScaleTransform.ScaleX = _zoomLevel; PdfScaleTransform.ScaleY = _zoomLevel; }
            UpdateZoomInfo();
        }

        private void BtnPageModeContinuous_Click(object sender, RoutedEventArgs e) => SetPageMode(false);
        private void BtnPageModeDouble_Click(object sender, RoutedEventArgs e) => SetPageMode(true);
        private void SetPageMode(bool isDouble)
        {
            _isDoublePage = isDouble; BtnPageModeContinuous.IsChecked = !isDouble; BtnPageModeDouble.IsChecked = isDouble;
            TxtViewMode.Text = isDouble ? "Ganda" : "Tunggal";
            if (_currentDocument != null) { if (_isDoublePage && _currentPage % 2 != 0) _currentPage = Math.Max(0, _currentPage - 1); RenderCurrentPages(); }
        }

        private void BtnToggleToc_Click(object sender, RoutedEventArgs e) => ToggleTocSidebar();
        private void BtnCloseToc_Click(object sender, RoutedEventArgs e) => CloseTocSidebar();
        private void TocTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { if (e.NewValue is PdfBookmark bookmark) GoToPage(bookmark.PageIndex); }
        private void ToggleTocSidebar() { if (_isTocOpen) CloseTocSidebar(); else OpenTocSidebar(); }
        private void OpenTocSidebar() { if (_isTocOpen) return; _isTocOpen = true; TocPanel.Visibility = Visibility.Visible; TocSplitter.Visibility = Visibility.Visible; var anim = new System.Windows.Media.Animation.DoubleAnimation { From = 0, To = 280, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } }; TocPanel.BeginAnimation(FrameworkElement.WidthProperty, anim); }
        private void CloseTocSidebar() { if (!_isTocOpen) return; _isTocOpen = false; var anim = new System.Windows.Media.Animation.DoubleAnimation { From = TocPanel.ActualWidth, To = 0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } }; anim.Completed += (s, e) => { if (!_isTocOpen) { TocPanel.Visibility = Visibility.Collapsed; TocSplitter.Visibility = Visibility.Collapsed; } }; TocPanel.BeginAnimation(FrameworkElement.WidthProperty, anim); }
        private void LoadTableOfContents()
        {
            if (_currentDocument == null) return;
            var bookmarks = _currentDocument.Bookmarks;
            if (bookmarks != null && bookmarks.Count > 0) { TocTreeView.ItemsSource = bookmarks; TocTreeView.Visibility = Visibility.Visible; TxtNoToc.Visibility = Visibility.Collapsed; }
            else { TocTreeView.ItemsSource = null; TocTreeView.Visibility = Visibility.Collapsed; TxtNoToc.Visibility = Visibility.Visible; }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { BtnBukaFile_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (ReaderContainer.Visibility != Visibility.Visible) return;
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control) { if (_currentDocument != null) ToggleTocSidebar(); e.Handled = true; return; }
            if ((e.Key == Key.OemPlus || e.Key == Key.Add) && Keyboard.Modifiers == ModifierKeys.Control) { ZoomIn(); e.Handled = true; return; }
            if ((e.Key == Key.OemMinus || e.Key == Key.Subtract) && Keyboard.Modifiers == ModifierKeys.Control) { ZoomOut(); e.Handled = true; return; }
            if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control) { if (_currentDocument != null) { _isFitWidth = false; _zoomLevel = 1.0; ApplyZoom(); } e.Handled = true; return; }
            if (e.Key == Key.PageUp || e.Key == Key.Left) { GoToPreviousPage(); e.Handled = true; return; }
            if (e.Key == Key.PageDown || e.Key == Key.Right) { GoToNextPage(); e.Handled = true; return; }
            if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.Control) { GoToPage(0); e.Handled = true; return; }
            if (e.Key == Key.End && Keyboard.Modifiers == ModifierKeys.Control) { if (_currentDocument != null) GoToPage(_pageCount - 1); e.Handled = true; return; }
            if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control) { if (_currentDocument != null) { _isFitWidth = !_isFitWidth; ApplyZoom(); } e.Handled = true; return; }
            if (e.Key == Key.Escape) { if (_currentDocument != null) { CloseDocument(); ShowHome(); } e.Handled = true; return; }
        }

        private void UpdatePageInfo()
        {
            if (_currentDocument == null) return;
            int startPage = _currentPage + 1; int endPage = _isDoublePage && _currentPage + 1 < _pageCount ? _currentPage + 2 : startPage;
            TxtPageInfo.Text = _isDoublePage && startPage != endPage ? $"Halaman {startPage}-{endPage} / {_pageCount}" : $"Halaman {startPage} / {_pageCount}";
            BtnPrevPage.IsEnabled = _currentPage > 0; BtnNextPage.IsEnabled = _currentPage + (_isDoublePage ? 2 : 1) < _pageCount;
        }

        private void UpdateZoomInfo()
        {
            if (_currentDocument == null) return;
            string zoomText = _isFitWidth ? "Fit Width" : $"{(int)(_zoomLevel * 100)}%";
            TxtZoomLevel.Text = zoomText; TxtZoomStatus.Text = zoomText;
        }

        private void EnableControls(bool enabled)
        {
            BtnPrevPage.IsEnabled = enabled; BtnNextPage.IsEnabled = enabled;
            BtnZoomIn.IsEnabled = enabled; BtnZoomOut.IsEnabled = enabled;
            BtnFitWidth.IsEnabled = enabled; BtnPageModeContinuous.IsEnabled = enabled;
            BtnPageModeDouble.IsEnabled = enabled; BtnToggleToc.IsEnabled = enabled;
        }

        protected override void OnClosed(EventArgs e) { CloseDocument(); base.OnClosed(e); }
    }
}