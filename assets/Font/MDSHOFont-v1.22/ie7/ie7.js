/* To avoid CSS expressions while still supporting IE 7 and IE 6, use this script */
/* The script tag referencing this file must be placed before the ending body tag. */

/* Use conditional comments in order to target IE 7 and older:
	<!--[if lt IE 8]><!-->
	<script src="ie7/ie7.js"></script>
	<!--<![endif]-->
*/

(function() {
	function addIcon(el, entity) {
		var html = el.innerHTML;
		el.innerHTML = '<span style="font-family: \'MDSHOFont\'">' + entity + '</span>' + html;
	}
	var icons = {
		'mdshofont_mdsho_s': '&#xe900;',
		'mdshofont_mdsho_o': '&#xe901;',
		'mdshofont_times_s': '&#xe902;',
		'mdshofont_times_o': '&#xe903;',
		'mdshofont_rename_s': '&#xe904;',
		'mdshofont_rename_o': '&#xe905;',
		'mdshofont_settings_s': '&#xe906;',
		'mdshofont_settings_o': '&#xe907;',
		'mdshofont_clone_s': '&#xe908;',
		'mdshofont_clone_o': '&#xe909;',
		'mdshofont_clones_s': '&#xe90a;',
		'mdshofont_clones_o': '&#xe90b;',
		'mdshofont_dock_left_s': '&#xe90c;',
		'mdshofont_dock_left_o': '&#xe90d;',
		'mdshofont_dock_top_s': '&#xe90e;',
		'mdshofont_dock_top_o': '&#xe90f;',
		'mdshofont_dock_right_s': '&#xe910;',
		'mdshofont_dock_right_o': '&#xe911;',
		'mdshofont_dock_bottom_s': '&#xe912;',
		'mdshofont_dock_bottom_o': '&#xe913;',
		'mdshofont_shortcuts_s': '&#xe914;',
		'mdshofont_shortcuts_o': '&#xe915;',
		'mdshofont_shortcut_s': '&#xe916;',
		'mdshofont_shortcut_o': '&#xe917;',
		'mdshofont_minus_s': '&#xe918;',
		'mdshofont_minus_o': '&#xe919;',
		'mdshofont_plus_s': '&#xe91a;',
		'mdshofont_plus_o': '&#xe91b;',
		'mdshofont_delete_s': '&#xe91c;',
		'mdshofont_delete_o': '&#xe91d;',
		'mdshofont_cut_s': '&#xe91e;',
		'mdshofont_cut_o': '&#xe91f;',
		'mdshofont_copy_s': '&#xe920;',
		'mdshofont_copy_o': '&#xe921;',
		'mdshofont_paste_s': '&#xe922;',
		'mdshofont_paste_o': '&#xe923;',
		'mdshofont_asc_s': '&#xe924;',
		'mdshofont_asc_o': '&#xe925;',
		'mdshofont_desc_s': '&#xe926;',
		'mdshofont_desc_o': '&#xe927;',
		'mdshofont_theme_s': '&#xe928;',
		'mdshofont_theme_o': '&#xe929;',
		'mdshofont_info_s': '&#xe92a;',
		'mdshofont_info_o': '&#xe92b;',
		'mdshofont_warning_s': '&#xe92c;',
		'mdshofont_warning_o': '&#xe92d;',
		'mdshofont_desktop_s': '&#xe92e;',
		'mdshofont_desktop_o': '&#xe92f;',
		'mdshofont_control_s': '&#xe930;',
		'mdshofont_control_o': '&#xe931;',
		'mdshofont_brush_s': '&#xe932;',
		'mdshofont_brush_o': '&#xe933;',
		'mdshofont_send_s': '&#xe934;',
		'mdshofont_send_o': '&#xe935;',
		'mdshofont_key_s': '&#xe936;',
		'mdshofont_key_o': '&#xe937;',
		'mdshofont_database_s': '&#xe938;',
		'mdshofont_database_o': '&#xe939;',
		'mdshofont_command_s': '&#xe93a;',
		'mdshofont_command_o': '&#xe93b;',
		'mdshofont_bookmark_s': '&#xe93c;',
		'mdshofont_bookmark_o': '&#xe93d;',
		'mdshofont_share_s': '&#xe93e;',
		'mdshofont_share_o': '&#xe93f;',
		'mdshofont_star_s': '&#xe940;',
		'mdshofont_star_o': '&#xe941;',
		'mdshofont_shield_s': '&#xe942;',
		'mdshofont_shield_o': '&#xe943;',
		'mdshofont_folder_s': '&#xe944;',
		'mdshofont_folder_o': '&#xe945;',
		'mdshofont_globe_s': '&#xe946;',
		'mdshofont_globe_o': '&#xe947;',
		'mdshofont_run_s': '&#xe948;',
		'mdshofont_run_o': '&#xe949;',
		'mdshofont_wizard_s': '&#xe94a;',
		'mdshofont_wizard_o': '&#xe94b;',
		'mdshofont_comment_s': '&#xe94c;',
		'mdshofont_comment_o': '&#xe94d;',
		'mdshofont_note_s': '&#xe94e;',
		'mdshofont_note_o': '&#xe94f;',
		'mdshofont_thumbtackleft_s': '&#xe950;',
		'mdshofont_thumbtackleft_o': '&#xe951;',
		'mdshofont_thumbtackdown_s': '&#xe952;',
		'mdshofont_thumbtackdown_o': '&#xe953;',
		'mdshofont_ok_s': '&#xe954;',
		'mdshofont_ok_o': '&#xe955;',
		'mdshofont_wraptext_s': '&#xe956;',
		'mdshofont_wraptext_o': '&#xe957;',
		'mdshofont_heart_s': '&#xe958;',
		'mdshofont_heart_o': '&#xe959;',
		'mdshofont_snippet_s': '&#xe95a;',
		'mdshofont_snippet_o': '&#xe95b;',
		'mdshofont_font_s': '&#xe95c;',
		'mdshofont_font_o': '&#xe95d;',
		'mdshofont_time_s': '&#xe95e;',
		'mdshofont_time_o': '&#xe95f;',
		'0': 0
		},
		els = document.getElementsByTagName('*'),
		i, c, el;
	for (i = 0; ; i += 1) {
		el = els[i];
		if(!el) {
			break;
		}
		c = el.className;
		c = c.match(/mdshofont_[^\s'"]+/);
		if (c && icons[c[0]]) {
			addIcon(el, icons[c[0]]);
		}
	}
}());
