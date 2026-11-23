%global debug_package %{nil}

Name:           quadclicker
Version:        %{qc_version}
Release:        1
Summary:        Native open-source auto-clicker (Qt6, X11 and Wayland)
License:        MIT
URL:            https://github.com/Quadstronaut/QuadClicker
Source0:        quadclicker-%{version}.tar.gz

BuildRequires:  cmake >= 3.20
BuildRequires:  ninja-build
BuildRequires:  gcc-c++
BuildRequires:  qt6-qtbase-devel
BuildRequires:  libXtst-devel
BuildRequires:  libXScrnSaver-devel
Requires:       qt6-qtbase-gui
Requires:       libXtst
Requires:       libXScrnSaver

%description
Configurable click rate, button and type, fixed or cursor position, stop
conditions, idle start, global hotkeys (X11), tray icon and a full CLI.
On Wayland, clicks go through /dev/uinput; this package loads the uinput
module at boot and installs a udev rule for the logged-in user.

%prep
%autosetup -n quadclicker-%{version}

%build
cmake -S linux -B build -G Ninja -DCMAKE_BUILD_TYPE=Release \
      -DCMAKE_INSTALL_PREFIX=%{_prefix} -DQUADCLICKER_BUILD_TESTS=OFF
cmake --build build

%install
DESTDIR=%{buildroot} cmake --install build --strip

%post
modprobe uinput 2>/dev/null || :
udevadm control --reload 2>/dev/null || :
udevadm trigger --subsystem-match=misc --sysname-match=uinput 2>/dev/null || :

%files
%license LICENSE
%{_bindir}/quadclicker
%{_datadir}/applications/quadclicker.desktop
%{_datadir}/metainfo/quadclicker.appdata.xml
%{_datadir}/icons/hicolor/scalable/apps/quadclicker.svg
%{_prefix}/lib/udev/rules.d/70-quadclicker-uinput.rules
%{_prefix}/lib/modules-load.d/quadclicker.conf

%changelog
* Mon Nov 23 2026 Quadstronaut <noreply@users.noreply.github.com> - %{version}-1
- Packaged by the QCLK-9 release workflow.
