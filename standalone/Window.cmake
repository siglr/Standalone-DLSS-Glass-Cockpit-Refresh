file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/AppUpdate.cs" GLASS_UPDATE_SOURCE)
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/NativeInstaller.cs" GLASS_INSTALL_SOURCE)
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/SimulatorDiscovery.cs" GLASS_DISCOVERY_SOURCE)
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/ReleaseTrust.cs" GLASS_TRUST_SOURCE)
set(GLASS_PUBLIC_KEY_FILE "${CMAKE_CURRENT_SOURCE_DIR}/standalone/ReleasePublicKey.cs" CACHE FILEPATH "Release verification public-key C# source")
option(GLASS_ENABLE_OFFICIAL_UPDATES "Allow this build to update from the official WeSimGlide release channel" OFF)
file(TO_NATIVE_PATH "${GLASS_PUBLIC_KEY_FILE}" GLASS_KEY_SOURCE)
set(GLASS_UPDATE_DEFINE)
if(NOT GLASS_ENABLE_OFFICIAL_UPDATES)
 set(GLASS_UPDATE_DEFINE /define:GLASS_NO_AUTO_UPDATE)
endif()
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/WsgTheme.cs" GLASS_THEME_SOURCE)
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/assets/WeSimGlide.ico" GLASS_ICON_SOURCE)
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/assets/WeSimGlide.png" GLASS_LOGO_SOURCE)
configure_file(standalone/ProductVersion.cs.in "${CMAKE_BINARY_DIR}/control/ProductVersion.cs" @ONLY)
file(TO_NATIVE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/standalone/GlassWindow.cs" GLASS_WINDOW_SOURCE)
file(TO_NATIVE_PATH "${CMAKE_BINARY_DIR}/control/ProductVersion.cs" GLASS_VERSION_SOURCE)
add_custom_command(OUTPUT "${CMAKE_BINARY_DIR}/control/Standalone-DLSS-Glass-Cockpit-Refresh.exe"
 COMMAND "${CMAKE_COMMAND}" -E make_directory "${CMAKE_BINARY_DIR}/control"
 COMMAND "${CMAKE_COMMAND}" -E env "LIB=" "${UVR_CSC}" /nologo /target:winexe /platform:x64 /optimize+ ${GLASS_UPDATE_DEFINE}
 "/win32icon:${GLASS_ICON_SOURCE}" "/resource:${GLASS_ICON_SOURCE},WeSimGlide.ico" "/resource:${GLASS_LOGO_SOURCE},WeSimGlide.png"
 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll
 "/out:${CMAKE_BINARY_DIR}/control/Standalone-DLSS-Glass-Cockpit-Refresh.exe"
 "${GLASS_THEME_SOURCE}" "${GLASS_WINDOW_SOURCE}" "${GLASS_VERSION_SOURCE}" "${GLASS_SETTINGS_SOURCE}" "${GLASS_UPDATE_SOURCE}" "${GLASS_INSTALL_SOURCE}" "${GLASS_DISCOVERY_SOURCE}" "${GLASS_TRUST_SOURCE}" "${GLASS_KEY_SOURCE}"
 COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${CMAKE_CURRENT_SOURCE_DIR}/standalone/VERSION" "${CMAKE_BINARY_DIR}/control/VERSION"
 DEPENDS standalone/assets/WeSimGlide.png standalone/WsgTheme.cs standalone/assets/WeSimGlide.ico standalone/AppUpdate.cs standalone/GlassWindow.cs standalone/NativeInstaller.cs standalone/SimulatorDiscovery.cs standalone/ReleaseTrust.cs "${GLASS_PUBLIC_KEY_FILE}" tools/GlassSettingsPanel.cs standalone/VERSION "${CMAKE_BINARY_DIR}/control/ProductVersion.cs" VERBATIM)
add_custom_target(glass_window ALL DEPENDS "${CMAKE_BINARY_DIR}/control/Standalone-DLSS-Glass-Cockpit-Refresh.exe")
