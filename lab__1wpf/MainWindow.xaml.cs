using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Lab1_WPF
{
    public partial class MainWindow : Window
    {
        private FileStream _mainFile;
        private FileStream _specFile;
        private short _dataLength = 20;
        private List<ProductComponent> _components = new List<ProductComponent>();
        private string _currentProjectPath;
        private string _currentSpecPath;
        private ProductComponent _selectedSpecComponent;

        private const int SPEC_ENTRY_SIZE = 11;
        private const int COMPONENT_HEADER_SIZE = 28;
        private const int SPEC_HEADER_SIZE = 8;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CreateProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "PRD Files|*.prd",
                    Title = "Создать новый проект",
                    DefaultExt = ".prd"
                };

                if (dialog.ShowDialog() == true)
                {
                    string prdPath = dialog.FileName;
                    string prsPath = Path.ChangeExtension(prdPath, ".prs");

                    var inputWindow = new Window
                    {
                        Title = "Параметры проекта",
                        Width = 300,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this
                    };

                    var grid = new Grid { Margin = new Thickness(10) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    grid.Children.Add(new TextBlock { Text = "Длина имени компонента:", Margin = new Thickness(0, 0, 0, 5) });
                    var txtDataLen = new TextBox { Text = "20", Margin = new Thickness(0, 0, 0, 10) };
                    Grid.SetRow(txtDataLen, 1);
                    grid.Children.Add(txtDataLen);

                    var btnOk = new Button { Content = "OK", Width = 75, HorizontalAlignment = HorizontalAlignment.Right };
                    Grid.SetRow(btnOk, 2);
                    btnOk.Click += (s, args) => { inputWindow.DialogResult = true; inputWindow.Close(); };
                    grid.Children.Add(btnOk);

                    inputWindow.Content = grid;

                    if (inputWindow.ShowDialog() == true)
                    {
                        if (short.TryParse(txtDataLen.Text, out _dataLength))
                        {
                            CreateProjectFiles(prdPath, prsPath);
                            _currentProjectPath = prdPath;
                            _currentSpecPath = prsPath;
                            txtStatus.Text = "Проект создан";
                            MessageBox.Show($"Проект успешно создан!\n\nФайлы:\n{prdPath}\n{prsPath}", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Неверный формат", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateProjectFiles(string prdPath, string prsPath)
        {
            using (var writer = new BinaryWriter(File.Open(prdPath, FileMode.Create)))
            {
                writer.Write(Encoding.ASCII.GetBytes("PS"));
                writer.Write(_dataLength);
                writer.Write(-1);  // first component = -1 (пусто)
                writer.Write(COMPONENT_HEADER_SIZE);  // free space = после заголовка
                byte[] specBytes = new byte[16];
                byte[] nameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(prsPath));
                Array.Copy(nameBytes, specBytes, Math.Min(nameBytes.Length, 16));
                writer.Write(specBytes);
            }

            using (var writer = new BinaryWriter(File.Open(prsPath, FileMode.Create)))
            {
                writer.Write(-1);  // first spec entry
                writer.Write(SPEC_HEADER_SIZE);  // free space
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "PRD Files|*.prd",
                    Title = "Открыть проект"
                };

                if (dialog.ShowDialog() == true)
                {
                    CloseFiles();

                    var files = OpenFiles(dialog.FileName);
                    _mainFile = files.main;
                    _specFile = files.spec;
                    _currentProjectPath = dialog.FileName;
                    _currentSpecPath = Path.Combine(Path.GetDirectoryName(dialog.FileName),
                        Path.GetFileNameWithoutExtension(dialog.FileName) + ".prs");

                    LoadComponents();
                    txtStatus.Text = $"Проект: {Path.GetFileName(dialog.FileName)} | Спецификация: {Path.GetFileName(_currentSpecPath)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (FileStream main, FileStream spec) OpenFiles(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            FileStream mainFs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            try
            {
                if (mainFs.Length < COMPONENT_HEADER_SIZE)
                    throw new InvalidDataException("Файл .prd повреждён");

                using (var reader = new BinaryReader(mainFs, Encoding.UTF8, leaveOpen: true))
                {
                    byte[] signature = reader.ReadBytes(2);
                    if (signature.Length != 2 || signature[0] != 'P' || signature[1] != 'S')
                        throw new InvalidDataException("Неверный формат PRD");

                    _dataLength = reader.ReadInt16();

                    mainFs.Position = 12;
                    byte[] nameBytes = reader.ReadBytes(16);
                    string specName = Encoding.UTF8.GetString(nameBytes).Trim('\0');

                    if (string.IsNullOrWhiteSpace(specName))
                        throw new InvalidDataException("Не указано имя файла спецификации");

                    string specPath = Path.Combine(Path.GetDirectoryName(path) ?? "", specName);
                    if (!File.Exists(specPath))
                        throw new FileNotFoundException($"Файл спецификации не найден: {specName}");

                    FileStream specFs = new FileStream(specPath, FileMode.Open, FileAccess.ReadWrite);
                    mainFs.Position = 0;
                    return (mainFs, specFs);
                }
            }
            catch
            {
                mainFs.Dispose();
                throw;
            }
        }

        private void CloseFiles()
        {
            try { _mainFile?.Close(); } catch { }
            try { _specFile?.Close(); } catch { }
            _mainFile = null;
            _specFile = null;
        }

        private void LoadComponents()
        {
            _components.Clear();
            if (_mainFile == null || _specFile == null)
            {
                dgComponents.ItemsSource = null;
                tvSpecification.Items.Clear();
                UpdateSpecSelector();
                return;
            }

            try
            {
                using (var br = new BinaryReader(_mainFile, Encoding.UTF8, true))
                {
                    if (_mainFile.Length < COMPONENT_HEADER_SIZE)
                    {
                        dgComponents.ItemsSource = null;
                        tvSpecification.Items.Clear();
                        UpdateSpecSelector();
                        return;
                    }

                    _mainFile.Position = 2;
                    short dataLen = br.ReadInt16();
                    int currentAddr = br.ReadInt32();

                    while (currentAddr != -1 && currentAddr < _mainFile.Length)
                    {
                        if (currentAddr + 9 + dataLen > _mainFile.Length)
                            break;

                        _mainFile.Position = currentAddr;
                        byte del = br.ReadByte();
                        int specPtr = br.ReadInt32();
                        int nextPtr = br.ReadInt32();
                        string name = Encoding.UTF8.GetString(br.ReadBytes(dataLen)).Trim('\0').Trim();

                        if (del == 0 && !string.IsNullOrWhiteSpace(name))
                        {
                            _components.Add(new ProductComponent
                            {
                                Name = name,
                                Address = currentAddr,
                                SpecPointer = specPtr,
                                Children = new List<ProductComponent>()
                            });
                        }
                        currentAddr = nextPtr;
                    }
                }

                LoadHierarchy();
                dgComponents.ItemsSource = null;
                dgComponents.ItemsSource = _components;
                UpdateSpecSelector();

                if (_components.Count > 0 && _selectedSpecComponent == null)
                    _selectedSpecComponent = _components[0];

                BuildSpecificationTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadHierarchy()
        {
            try
            {
                var addrToComponent = _components.ToDictionary(c => c.Address);
                foreach (var comp in _components) comp.Children.Clear();

                foreach (var parent in _components)
                {
                    if (parent.SpecPointer == -1 || parent.SpecPointer < SPEC_HEADER_SIZE)
                        continue;

                    int specAddr = parent.SpecPointer;
                    var visited = new HashSet<int>();

                    while (specAddr != -1 && specAddr < _specFile.Length)
                    {
                        if (visited.Contains(specAddr)) break;
                        visited.Add(specAddr);

                        if (specAddr + SPEC_ENTRY_SIZE > _specFile.Length) break;

                        _specFile.Position = specAddr;
                        byte del = (byte)_specFile.ReadByte();
                        int childAddr = ReadInt32(_specFile);
                        short quantity = ReadInt16(_specFile);
                        int nextSpec = ReadInt32(_specFile);

                        if (del == 0 && addrToComponent.ContainsKey(childAddr))
                        {
                            var child = addrToComponent[childAddr];
                            if (!parent.Children.Contains(child))
                                parent.Children.Add(child);
                        }
                        specAddr = nextSpec;
                    }
                }

                foreach (var comp in _components)
                    comp.Type = comp.Children.Count > 0 ? "Узел/Изделие" : "Деталь";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка иерархии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private short ReadInt16(Stream stream)
        {
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            return BitConverter.ToInt16(buffer, 0);
        }

        private int ReadInt32(Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        private void WriteInt16(Stream stream, short value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            stream.Write(buffer, 0, 2);
        }

        private void WriteInt32(Stream stream, int value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            stream.Write(buffer, 0, 4);
        }

        private void UpdateSpecSelector()
        {
            var cmbSpec = FindName("cmbSpecSelector") as ComboBox;
            if (cmbSpec != null)
            {
                cmbSpec.Items.Clear();
                foreach (var comp in _components) cmbSpec.Items.Add(comp);
                if (_components.Count > 0)
                {
                    cmbSpec.SelectedIndex = 0;
                    _selectedSpecComponent = _components[0];
                }
            }
        }

        private void BuildSpecificationTree()
        {
            tvSpecification.Items.Clear();
            if (_selectedSpecComponent == null)
            {
                txtStatus.Text += " | Спецификация не выбрана";
                return;
            }

            if (_selectedSpecComponent.Children.Count > 0)
            {
                var rootItem = CreateTreeViewItem(_selectedSpecComponent);
                tvSpecification.Items.Add(rootItem);
                rootItem.IsExpanded = true;
                txtStatus.Text += $" | Показана спецификация: {_selectedSpecComponent.Name}";
            }
            else
            {
                tvSpecification.Items.Add(new TreeViewItem
                {
                    Header = $"{_selectedSpecComponent.Name} (Деталь - нет состава)"
                });
                txtStatus.Text += $" | {_selectedSpecComponent.Name} - это деталь";
            }
        }

        private TreeViewItem CreateTreeViewItem(ProductComponent comp)
        {
            var item = new TreeViewItem
            {
                Header = $"{comp.Name} ({comp.Type})",
                IsExpanded = true
            };
            foreach (var child in comp.Children)
                item.Items.Add(CreateTreeViewItem(child));
            return item;
        }

        private void AddComponent_Click(object sender, RoutedEventArgs e)
        {
            if (_mainFile == null)
            {
                MessageBox.Show("Сначала откройте или создайте проект", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new AddComponentWindow();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    AddComponentToFile(dialog.ComponentName);
                    LoadComponents();
                    txtStatus.Text = $"Компонент '{dialog.ComponentName}' добавлен";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddComponentToFile(string name)
        {
            using (var br = new BinaryReader(_mainFile, Encoding.UTF8, true))
            using (var bw = new BinaryWriter(_mainFile, Encoding.UTF8, true))
            {
                _mainFile.Position = 2;
                short dataLen = br.ReadInt16();
                int firstComponent = br.ReadInt32();  // указатель на первый компонент

                _mainFile.Position = 8;
                int freeAddr = br.ReadInt32();  // указатель на свободное место

                if (freeAddr < COMPONENT_HEADER_SIZE || freeAddr >= _mainFile.Length)
                    freeAddr = (int)_mainFile.Length;

                int recordSize = 9 + dataLen;

                int lastComponentAddr = -1;
                int currentAddr = firstComponent;

                while (currentAddr != -1 && currentAddr < _mainFile.Length)
                {
                    if (currentAddr + 9 + dataLen > _mainFile.Length)
                        break;

                    _mainFile.Position = currentAddr;
                    byte del = br.ReadByte();

                    if (del == 0)  // только активные компоненты
                        lastComponentAddr = currentAddr;

                    _mainFile.Position = currentAddr + 5;  // переходим к next pointer
                    currentAddr = br.ReadInt32();
                }

                _mainFile.Position = freeAddr;
                bw.Write((byte)0);      // del = 0 (активен)
                bw.Write(-1);           // spec pointer = -1 (нет спецификации)
                bw.Write(-1);           // next pointer = -1 (пока последний)

                byte[] nameBytes = new byte[dataLen];
                byte[] nameData = Encoding.UTF8.GetBytes(name);
                Array.Copy(nameData, nameBytes, Math.Min(nameData.Length, dataLen));
                bw.Write(nameBytes);

                if (lastComponentAddr == -1)
                {
                    // Это первый компонент — обновляем заголовок
                    _mainFile.Position = 4;
                    bw.Write(freeAddr);
                }
                else
                {
                    _mainFile.Position = lastComponentAddr + 5;  // позиция next pointer
                    bw.Write(freeAddr);
                }

                _mainFile.Position = 8;
                bw.Write(freeAddr + recordSize);
            }
        }

        private void DeleteComponent_Click(object sender, RoutedEventArgs e)
        {
            if (_mainFile == null || dgComponents.SelectedItem == null)
            {
                MessageBox.Show("Выберите компонент", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var component = (ProductComponent)dgComponents.SelectedItem;
            var result = MessageBox.Show($"Удалить '{component.Name}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                DeleteComponentFromFile(component.Name);
                LoadComponents();
                txtStatus.Text = $"Компонент '{component.Name}' удалён";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteComponentFromFile(string name)
        {
            int addr = FindComponentAddress(name, out _);
            if (addr == -1) throw new Exception("Компонент не найден");

            // Проверяем, не используется ли компонент в спецификациях
            if (_specFile != null && _specFile.Length >= SPEC_HEADER_SIZE)
            {
                _specFile.Position = 0;
                int sCurr = ReadInt32(_specFile);
                var visited = new HashSet<int>();

                while (sCurr != -1 && sCurr < _specFile.Length)
                {
                    if (visited.Contains(sCurr)) break;
                    visited.Add(sCurr);
                    if (sCurr + SPEC_ENTRY_SIZE > _specFile.Length) break;

                    _specFile.Position = sCurr;
                    byte del = (byte)_specFile.ReadByte();
                    int compAddr = ReadInt32(_specFile);
                    int nextSpec = ReadInt32(_specFile);

                    if (del == 0 && compAddr == addr)
                        throw new Exception($"Компонент '{name}' используется в спецификациях!");

                    sCurr = nextSpec;
                }
            }

            using (var bw = new BinaryWriter(_mainFile, Encoding.UTF8, true))
            {
                _mainFile.Position = addr;
                bw.Write((byte)255);  // deleted flag

                _mainFile.Position = 8;
                int oldFree = ReadInt32(_mainFile);
                _mainFile.Position = addr + 1;
                WriteInt32(_mainFile, oldFree);  
                _mainFile.Position = 8;
                WriteInt32(_mainFile, addr);     
            }
        }

        private void AddToSpec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mainFile == null || _components.Count < 2)
                {
                    MessageBox.Show("Нужно минимум 2 компонента", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new AddToSpecWindow(_components, _components);
                if (dialog.ShowDialog() == true)
                {
                    if (WouldCreateCycle(dialog.ParentName, dialog.ChildName))
                    {
                        MessageBox.Show("Нельзя создать циклическую ссылку!\n" +
                            $"{dialog.ChildName} уже содержит {dialog.ParentName} в своей иерархии.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    AddToSpecification(dialog.ParentName, dialog.ChildName);
                    LoadComponents();
                    txtStatus.Text = $"Связь: {dialog.ParentName} → {dialog.ChildName}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool WouldCreateCycle(string parentName, string childName)
        {
            int parentAddr = FindComponentAddress(parentName, out _);
            int childAddr = FindComponentAddress(childName, out _);
            if (parentAddr == -1 || childAddr == -1) return false;
            if (parentAddr == childAddr) return true;

            return HasPath(childAddr, parentAddr, new HashSet<int>());
        }

        private bool HasPath(int fromAddr, int toAddr, HashSet<int> visited)
        {
            if (fromAddr == toAddr) return true;
            if (visited.Contains(fromAddr)) return false;
            visited.Add(fromAddr);

            var comp = _components.FirstOrDefault(c => c.Address == fromAddr);
            if (comp == null) return false;

            if (comp.SpecPointer != -1 && comp.SpecPointer >= SPEC_HEADER_SIZE)
            {
                int specAddr = comp.SpecPointer;
                var specVisited = new HashSet<int>();

                while (specAddr != -1 && specAddr < _specFile.Length)
                {
                    if (specVisited.Contains(specAddr)) break;
                    specVisited.Add(specAddr);
                    if (specAddr + SPEC_ENTRY_SIZE > _specFile.Length) break;

                    _specFile.Position = specAddr;
                    byte del = (byte)_specFile.ReadByte();
                    int childAddr = ReadInt32(_specFile);
                    int nextSpec = ReadInt32(_specFile);

                    if (del == 0 && HasPath(childAddr, toAddr, visited))
                        return true;

                    specAddr = nextSpec;
                }
            }
            return false;
        }

        private void AddToSpecification(string parent, string child)
        {
            int pAddr = FindComponentAddress(parent, out _);
            int cAddr = FindComponentAddress(child, out _);

            if (pAddr == -1 || cAddr == -1)
                throw new Exception("Компонент не найден");
            if (pAddr == cAddr)
                throw new Exception("Компонент не может быть родителем самого себя");

            using (var bwMain = new BinaryWriter(_mainFile, Encoding.UTF8, true))
            using (var bwSpec = new BinaryWriter(_specFile, Encoding.UTF8, true))
            {
                _mainFile.Position = pAddr + 1;
                int oldSpecHead = ReadInt32(_mainFile);

                _specFile.Position = 4;
                int freeAddr = ReadInt32(_specFile);

                if (freeAddr < SPEC_HEADER_SIZE || freeAddr >= _specFile.Length)
                {
                    freeAddr = (int)_specFile.Length;
                }

                _specFile.Position = freeAddr;
                bwSpec.Write((byte)0);
                bwSpec.Write(cAddr);
                bwSpec.Write((short)1);
                bwSpec.Write(oldSpecHead);

                _mainFile.Position = pAddr + 1;
                bwMain.Write(freeAddr);

                _specFile.Position = 4;
                bwSpec.Write((int)_specFile.Length);
            }
        }

        private void DeleteFromSpec_Click(object sender, RoutedEventArgs e)
        {
            if (_mainFile == null || dgComponents.SelectedItem == null)
            {
                MessageBox.Show("Выберите компонент", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var component = (ProductComponent)dgComponents.SelectedItem;
            if (component.Children.Count == 0)
            {
                MessageBox.Show("У компонента нет связей", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Удалить все связи '{component.Name}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                DeleteSpecification(component.Name);
                LoadComponents();
                txtStatus.Text = $"Связи '{component.Name}' удалены";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSpecification(string componentName)
        {
            int addr = FindComponentAddress(componentName, out _);
            if (addr == -1) throw new Exception("Компонент не найден");

            using (var bwMain = new BinaryWriter(_mainFile, Encoding.UTF8, true))
            using (var bwSpec = new BinaryWriter(_specFile, Encoding.UTF8, true))
            {
                _mainFile.Position = addr + 1;
                int specPtr = ReadInt32(_mainFile);

                if (specPtr != -1)
                {
                    int current = specPtr;
                    var visited = new HashSet<int>();

                    while (current != -1 && current < _specFile.Length)
                    {
                        if (visited.Contains(current)) break;
                        visited.Add(current);
                        if (current + SPEC_ENTRY_SIZE > _specFile.Length) break;

                        _specFile.Position = current;
                        byte del = (byte)_specFile.ReadByte();
                        if (del == 0)
                        {
                            _specFile.Position = current;
                            bwSpec.Write((byte)255);

                            _specFile.Position = 4;
                            int oldFree = ReadInt32(_specFile);
                            _specFile.Position = current + 1;
                            WriteInt32(_specFile, oldFree);
                            _specFile.Position = 4;
                            WriteInt32(_specFile, current);
                        }

                        _specFile.Position = current + 7;
                        current = ReadInt32(_specFile);
                    }

                    _mainFile.Position = addr + 1;
                    bwMain.Write(-1);
                }
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (_mainFile == null)
            {
                MessageBox.Show("Откройте проект", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                RestoreComponents();
                LoadComponents();
                txtStatus.Text = "Компоненты восстановлены и отсортированы";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreComponents()
        {
            using (var br = new BinaryReader(_mainFile, Encoding.UTF8, true))
            using (var bw = new BinaryWriter(_mainFile, Encoding.UTF8, true))
            {
                _mainFile.Position = 2;
                short dLen = br.ReadInt16();

                var entries = new List<MainFileEntry>();

                int curr = COMPONENT_HEADER_SIZE;
                while (curr + 9 + dLen <= _mainFile.Length)
                {
                    _mainFile.Position = curr;
                    byte del = br.ReadByte();
                    int sPtr = ReadInt32(_mainFile);
                    ReadInt32(_mainFile);
                    byte[] rawName = br.ReadBytes(dLen);
                    string name = Encoding.UTF8.GetString(rawName).Trim('\0').Trim();

                    if (del == 0 && !string.IsNullOrWhiteSpace(name))
                    {
                        entries.Add(new MainFileEntry
                        {
                            Addr = curr,
                            Name = name,
                            SpecPtr = sPtr,
                            Data = rawName
                        });
                    }
                    curr += (9 + dLen);
                }

                if (entries.Count == 0) return;
                var sorted = entries.OrderBy(e => e.Name).ToList();

                _mainFile.Position = 4;
                bw.Write(COMPONENT_HEADER_SIZE);

                for (int i = 0; i < sorted.Count; i++)
                {
                    int newAddr = COMPONENT_HEADER_SIZE + i * (9 + dLen);
                    int nextAddr = (i == sorted.Count - 1) ? -1 : newAddr + (9 + dLen);

                    _mainFile.Position = newAddr;
                    bw.Write((byte)0);
                    bw.Write(sorted[i].SpecPtr);
                    bw.Write(nextAddr);
                    bw.Write(sorted[i].Data);
                }
                _mainFile.Position = 8;
                bw.Write(COMPONENT_HEADER_SIZE + sorted.Count * (9 + dLen));
            }
        }

        private void Truncate_Click(object sender, RoutedEventArgs e)
        {
            if (_mainFile == null || _specFile == null)
            {
                MessageBox.Show("Откройте проект", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show("Физически удалить помеченные?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                TruncateFiles();
                LoadComponents();
                txtStatus.Text = "Физическое удаление завершено";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TruncateFiles()
        {
            if (_mainFile == null || _specFile == null)
                return;

            using (var brM = new BinaryReader(_mainFile, Encoding.UTF8, true))
            {
                _mainFile.Position = 2;
                short dLen = brM.ReadInt16();

                var mainList = new List<(int oldAddr, byte[] data, int specPtr)>();
                var addrMap = new Dictionary<int, int>();

                int recordSize = 9 + dLen;
                int curr = COMPONENT_HEADER_SIZE;

                while (curr + recordSize <= _mainFile.Length)
                {
                    _mainFile.Position = curr;
                    byte del = brM.ReadByte();
                    int sPtr = ReadInt32(_mainFile);
                    ReadInt32(_mainFile); // пропускаем NextPtr
                    byte[] nameData = new byte[dLen];
                    brM.Read(nameData, 0, dLen);

                    if (del == 0)
                    {
                        mainList.Add((curr, nameData, sPtr));
                    }

                    curr += recordSize;
                }

                _mainFile.SetLength(0);
                using (var bwM = new BinaryWriter(_mainFile, Encoding.UTF8, true))
                {
                    bwM.Write(Encoding.ASCII.GetBytes("PS"));
                    bwM.Write(dLen);
                    bwM.Write(mainList.Count > 0 ? COMPONENT_HEADER_SIZE : -1);
                    bwM.Write(0);

                    byte[] specNameBytes = new byte[16];
                    Encoding.UTF8.GetBytes(Path.GetFileName(_specFile.Name)).CopyTo(specNameBytes, 0);
                    bwM.Write(specNameBytes);

                    for (int i = 0; i < mainList.Count; i++)
                    {
                        int newAddr = COMPONENT_HEADER_SIZE + i * recordSize;
                        int nextAddr = (i == mainList.Count - 1) ? -1 : newAddr + recordSize;

                        addrMap[mainList[i].oldAddr] = newAddr;

                        _mainFile.Position = newAddr;
                        bwM.Write((byte)0);
                        bwM.Write(mainList[i].specPtr);
                        bwM.Write(nextAddr);
                        bwM.Write(mainList[i].data);
                    }

                    _mainFile.Position = 8;
                    bwM.Write(mainList.Count > 0 ? COMPONENT_HEADER_SIZE + mainList.Count * recordSize : COMPONENT_HEADER_SIZE);
                }

                using (var brS = new BinaryReader(_specFile, Encoding.UTF8, true))
                using (var bwS = new BinaryWriter(_specFile, Encoding.UTF8, true))
                {
                    long sPos = SPEC_HEADER_SIZE;
                    while (sPos + SPEC_ENTRY_SIZE <= _specFile.Length)
                    {
                        _specFile.Position = sPos;
                        byte del = (byte)_specFile.ReadByte();

                        _specFile.Position = sPos + 1;
                        int oldCompAddr = brS.ReadInt32();

                        if (del == 0 && addrMap.TryGetValue(oldCompAddr, out int newCompAddr))
                        {
                            _specFile.Position = sPos + 1;
                            bwS.Write(newCompAddr);
                        }

                        sPos += SPEC_ENTRY_SIZE;
                    }
                }
            }
        }

        private void PrintAll_Click(object sender, RoutedEventArgs e)
        {
            if (_components?.Count == 0)
            {
                MessageBox.Show("Нет компонентов", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("СПИСОК КОМПОНЕНТОВ");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();
            sb.AppendLine($"{"Наименование",-25} | {"Тип",-15} | Состав");
            sb.AppendLine(new string('-', 60));

            foreach (var c in _components)
            {
                string children = c.Children.Count > 0 ? string.Join(", ", c.Children.Select(x => x.Name)) : "—";
                sb.AppendLine($"{c.Name,-25} | {c.Type,-15} | {children}");
            }
            sb.AppendLine($"\nВсего: {_components.Count} компонент(ов)");

            MessageBox.Show(sb.ToString(), "Все компоненты", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            CloseFiles();
            Close();
        }

        private void CmbSpecSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cmb && cmb.SelectedItem is ProductComponent comp)
            {
                _selectedSpecComponent = comp;
                BuildSpecificationTree();
            }
        }

        private int FindComponentAddress(string name, out short dataLen)
        {
            using (var br = new BinaryReader(_mainFile, Encoding.UTF8, true))
            {
                _mainFile.Position = 2;
                dataLen = br.ReadInt16();
                int addr = br.ReadInt32();

                while (addr != -1 && addr < _mainFile.Length)
                {
                    if (addr + 9 + dataLen > _mainFile.Length) break;
                    _mainFile.Position = addr;
                    byte del = br.ReadByte();
                    _mainFile.Position = addr + 9;
                    string entryName = Encoding.UTF8.GetString(br.ReadBytes(dataLen)).Trim('\0').Trim();
                    if (del == 0 && entryName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return addr;
                    _mainFile.Position = addr + 5;
                    addr = br.ReadInt32();
                }
            }
            return -1;
        }

        private class MainFileEntry
        {
            public int Addr { get; set; }
            public string Name { get; set; }
            public int SpecPtr { get; set; }
            public byte[] Data { get; set; }
        }

       
    }

  

    public class ProductComponent
    {
        public string Name { get; set; }
        public string Type { get; set; } = "Деталь";
        public int Address { get; set; }
        public int SpecPointer { get; set; } = -1;
        public List<ProductComponent> Children { get; set; } = new List<ProductComponent>();
        public override string ToString() => Name;
    }

   


   

    public class AddComponentWindow : Window
    {
        private TextBox txtName;
        public string ComponentName { get; private set; }

        public AddComponentWindow()
        {
            Title = "Добавить компонент";
            Width = 350; Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = "Наименование компонента:", Margin = new Thickness(0, 0, 0, 5) });
            txtName = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtName, 1);
            grid.Children.Add(txtName);

            var hint = new TextBlock
            {
                Text = "Все компоненты создаются как Деталь.\nПри добавлении в спецификацию родитель станет Узлом/Изделием.",
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Brushes.Gray,
                FontSize = 11
            };
            Grid.SetRow(hint, 2);
            grid.Children.Add(hint);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnOk = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Click += (s, e) => { SaveAndClose(); };
            var btnCancel = new Button { Content = "Отмена", Width = 75 };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            panel.Children.Add(btnOk);
            panel.Children.Add(btnCancel);
            grid.Children.Add(panel);
            Content = grid;
        }

        private void SaveAndClose()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Введите наименование", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ComponentName = txtName.Text.Trim();
            DialogResult = true;
            Close();
        }
    }

    public class AddToSpecWindow : Window
    {
        private ComboBox cmbParent, cmbChild;
        public string ParentName { get; private set; }
        public string ChildName { get; private set; }

        public AddToSpecWindow(List<ProductComponent> parents, List<ProductComponent> children)
        {
            Title = "Добавить в спецификацию";
            Width = 450; Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(10) };
            for (int i = 0; i < 6; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = "Родитель (станет Узлом/Изделием):", FontWeight = FontWeights.Bold });
            cmbParent = new ComboBox { Margin = new Thickness(0, 5, 0, 5) };
            foreach (var c in parents) cmbParent.Items.Add(c);
            Grid.SetRow(cmbParent, 1);
            grid.Children.Add(cmbParent);

            Grid.SetRow(new TextBlock { Text = "Дочерний компонент:", Margin = new Thickness(0, 10, 0, 5) }, 2);
            cmbChild = new ComboBox { Margin = new Thickness(0, 5, 0, 10) };
            foreach (var c in children) cmbChild.Items.Add(c);
            Grid.SetRow(cmbChild, 3);
            grid.Children.Add(cmbChild);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Click += (s, e) => { SaveAndClose(); };
            var btnCancel = new Button { Content = "Отмена", Width = 75 };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            panel.Children.Add(btnOk);
            panel.Children.Add(btnCancel);
            Grid.SetRow(panel, 4);
            grid.Children.Add(panel);
            Content = grid;
        }

        private void SaveAndClose()
        {
            if (cmbParent.SelectedItem == null || cmbChild.SelectedItem == null)
            {
                MessageBox.Show("Выберите оба компонента", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbParent.SelectedItem == cmbChild.SelectedItem)
            {
                MessageBox.Show("Компоненты должны быть разными", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ParentName = ((ProductComponent)cmbParent.SelectedItem).Name;
            ChildName = ((ProductComponent)cmbChild.SelectedItem).Name;
            DialogResult = true;
            Close();
        }
    }

}