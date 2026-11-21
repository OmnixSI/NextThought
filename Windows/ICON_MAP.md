# 图标对照（ICON_MAP）

Mac 使用 SF Symbols，Windows 使用随包的 **Fluent System Icons**（`Assets/Fonts/FluentSystemIcons-Regular.ttf`，MIT，
Microsoft；系统 `Segoe Fluent Icons` 作为回退）。字形的码点来自 Fluent System Icons 的 glyph map（24 px regular
优先，个别符号取语义最接近的 Fluent 名）。资源在 `Styles/Icons.axaml`（`IconFont` + `Icon<Name>`），本文件覆盖
`Sources/NextThought/` 里全部 57 个 `Image(systemName:)` 符号。

> D7 状态：**已实现、未验证**。所有视图图标都已改成 `{StaticResource IconXxx}`（Views 下不再有 `&#xE…` 字形），
> SceneGlyph 也使用同一张表；码点已换成 Fluent System Icons 的正式码点，并排像素对照仍待实机。

## `Image(systemName:)` 符号

| SF Symbol | Fluent 名称 | 字形 | 资源 |
|---|---|---|---|
| `archivebox` | `ic_fluent_archive_24_regular` | `U+F13A` | `IconArchivebox` |
| `arrow.clockwise` | `ic_fluent_arrow_clockwise_24_regular` | `U+F13E` | `IconArrowClockwise` |
| `arrow.right` | `ic_fluent_arrow_right_24_regular` | `U+F182` | `IconArrowRight` |
| `arrow.up.circle.fill` | `ic_fluent_arrow_circle_up_24_regular` | `U+E0A3` | `IconArrowUpCircleFill` |
| `arrow.up.right` | `ic_fluent_arrow_up_right_24_regular` | `U+F1A3` | `IconArrowUpRight` |
| `bolt` | `ic_fluent_flash_24_regular` | `U+E619` | `IconBolt` |
| `bolt.fill` | `ic_fluent_flash_24_regular` | `U+E619` | `IconBoltFill` |
| `camera.viewfinder` | `ic_fluent_camera_24_regular` | `U+F255` | `IconCameraViewfinder` |
| `captions.bubble` | `ic_fluent_closed_caption_24_regular` | `U+F2E3` | `IconCaptionsBubble` |
| `checkmark` | `ic_fluent_checkmark_24_regular` | `U+F295` | `IconCheckmark` |
| `chevron.down` | `ic_fluent_chevron_down_24_regular` | `U+F2A4` | `IconChevronDown` |
| `chevron.left` | `ic_fluent_chevron_left_24_regular` | `U+F2AB` | `IconChevronLeft` |
| `chevron.right` | `ic_fluent_chevron_right_24_regular` | `U+F2B1` | `IconChevronRight` |
| `chevron.up` | `ic_fluent_chevron_up_24_regular` | `U+F2B7` | `IconChevronUp` |
| `circle` | `ic_fluent_circle_24_regular` | `U+F2BC` | `IconCircle` |
| `circle.fill` | `ic_fluent_circle_24_regular` | `U+F2BC` | `IconCircleFill` |
| `display` | `ic_fluent_tv_24_regular` | `U+EDC3` | `IconDisplay` |
| `doc.plaintext` | `ic_fluent_document_24_regular` | `U+F379` | `IconDocPlaintext` |
| `doc.richtext` | `ic_fluent_document_text_24_regular` | `U+E558` | `IconDocRichtext` |
| `ellipsis` | `ic_fluent_more_horizontal_24_regular` | `U+E825` | `IconEllipsis` |
| `exclamationmark.circle` | `ic_fluent_error_circle_24_regular` | `U+F3F2` | `IconExclamationmarkCircle` |
| `exclamationmark.triangle` | `ic_fluent_warning_24_regular` | `U+F86A` | `IconExclamationmarkTriangle` |
| `exclamationmark.triangle.fill` | `ic_fluent_warning_24_regular` | `U+F86A` | `IconExclamationmarkTriangleFill` |
| `folder` | `ic_fluent_folder_24_regular` | `U+F419` | `IconFolder` |
| `gearshape` | `ic_fluent_settings_24_regular` | `U+F6AA` | `IconGearshape` |
| `globe` | `ic_fluent_globe_24_regular` | `U+F45B` | `IconGlobe` |
| `info.circle` | `ic_fluent_info_24_regular` | `U+F4A4` | `IconInfoCircle` |
| `iphone` | `ic_fluent_phone_24_regular` | `U+F5E1` | `IconIphone` |
| `key` | `ic_fluent_key_24_regular` | `U+F4B7` | `IconKey` |
| `lock` | `ic_fluent_lock_closed_24_regular` | `U+E790` | `IconLock` |
| `magnifyingglass` | `ic_fluent_search_24_regular` | `U+F690` | `IconMagnifyingglass` |
| `minus.circle` | `ic_fluent_subtract_circle_24_regular` | `U+F7B1` | `IconMinusCircle` |
| `moon.zzz` | `ic_fluent_weather_moon_24_regular` | `U+F87E` | `IconMoonZzz` |
| `person.fill` | `ic_fluent_person_24_regular` | `U+F5BE` | `IconPersonFill` |
| `photo` | `ic_fluent_image_24_regular` | `U+F489` | `IconPhoto` |
| `pin.fill` | `ic_fluent_pin_24_regular` | `U+F602` | `IconPinFill` |
| `plus` | `ic_fluent_add_24_regular` | `U+F10A` | `IconPlus` |
| `qrcode` | `ic_fluent_qr_code_24_regular` | `U+F635` | `IconQrcode` |
| `questionmark.bubble` | `ic_fluent_chat_help_24_regular` | `U+F28A` | `IconQuestionmarkBubble` |
| `quote.bubble` | `ic_fluent_comment_quote_24_regular` | `U+F06ED` | `IconQuoteBubble` |
| `rectangle.on.rectangle` | `ic_fluent_window_multiple_24_regular` | `U+F0B61` | `IconRectangleOnRectangle` |
| `rectangle.topthird.inset.filled` | `ic_fluent_panel_top_gallery_24_regular` | `U+F0313` | `IconRectangleTopthirdInsetFilled` |
| `server.rack` | `ic_fluent_server_24_regular` | `U+F76C` | `IconServerRack` |
| `sidebar.left` | `ic_fluent_panel_left_24_regular` | `U+E8B1` | `IconSidebarLeft` |
| `sparkle` | `ic_fluent_sparkle_24_regular` | `U+EB34` | `IconSparkle` |
| `sparkles` | `ic_fluent_sparkle_24_regular` | `U+EB34` | `IconSparkles` |
| `speaker.wave.2` | `ic_fluent_speaker_2_24_regular` | `U+EB43` | `IconSpeakerWave2` |
| `star.fill` | `ic_fluent_star_24_regular` | `U+F710` | `IconStarFill` |
| `text.magnifyingglass` | `ic_fluent_search_24_regular` | `U+F690` | `IconTextMagnifyingglass` |
| `text.quote` | `ic_fluent_text_quote_24_regular` | `U+F801` | `IconTextQuote` |
| `textformat.size.larger` | `ic_fluent_text_font_size_24_regular` | `U+F7E7` | `IconTextformatSizeLarger` |
| `textformat.size.smaller` | `ic_fluent_text_font_size_24_regular` | `U+F7E7` | `IconTextformatSizeSmaller` |
| `trash` | `ic_fluent_delete_24_regular` | `U+F34D` | `IconTrash` |
| `tray.and.arrow.down` | `ic_fluent_arrow_download_24_regular` | `U+F151` | `IconTrayAndArrowDown` |
| `waveform` | `ic_fluent_pulse_24_regular` | `U+E9DD` | `IconWaveform` |
| `xmark` | `ic_fluent_dismiss_24_regular` | `U+F36A` | `IconXmark` |
| `xmark.circle.fill` | `ic_fluent_dismiss_circle_24_regular` | `U+F36E` | `IconXmarkCircleFill` |

## 场景主题与 Windows 专用图标

这些符号来自 `Stage.swift` 的 `SceneTheme.symbol`（不是 `Image(systemName:)` 的静态参数）或 Windows 外壳自己的
图标；它们同样只通过 `Styles/Icons.axaml` 引用。

| 触发 | Fluent 名称 | 字形 | 资源 |
|---|---|---|---|
| book（讲座场景） | `ic_fluent_book_24_regular` | `U+F8FE` | `IconBook` |
| briefcase（工作会议场景） | `ic_fluent_briefcase_24_regular` | `U+F1FD` | `IconBriefcase` |
| bubble.left.and.bubble.right（讨论场景） | `ic_fluent_comment_multiple_24_regular` | `U+F308` | `IconBubbleLeftAndBubbleRight` |
| 自动收尾倒计时 | `ic_fluent_clock_24_regular` | `U+F2DE` | `IconClock` |
| flask（组会场景） | `ic_fluent_beaker_24_regular` | `U+F1D8` | `IconFlask` |
| 打开数据目录 | `ic_fluent_folder_open_24_regular` | `U+F42F` | `IconFolderOpen` |
| graduationcap（汇报/答辩场景） | `ic_fluent_hat_graduation_24_regular` | `U+E6E7` | `IconGraduationcap` |
| Windows 旧侧栏：历史 | `ic_fluent_history_24_regular` | `U+F47F` | `IconHistory` |
| Windows 旧侧栏：首页 | `ic_fluent_home_24_regular` | `U+F481` | `IconHouse` |
| 教练提示 | `ic_fluent_lightbulb_24_regular` | `U+F4D7` | `IconLightbulb` |
| Windows 旧侧栏：会议 | `ic_fluent_text_bullet_list_24_regular` | `U+F0291` | `IconListBullet` |
| person.2（客户场景） | `ic_fluent_people_24_regular` | `U+F5A9` | `IconPerson2` |
| person.3（小组作业场景） | `ic_fluent_people_24_regular` | `U+F5A9` | `IconPerson3` |
| person.crop.rectangle（场景图标） | `ic_fluent_person_board_24_regular` | `U+F5CE` | `IconPersonCropRectangle` |
| slider.horizontal.3（自定义场景） | `ic_fluent_options_24_regular` | `U+F588` | `IconSliderHorizontal3` |
| 加载中状态 | `ic_fluent_arrow_sync_24_regular` | `U+F191` | `IconSync` |

## 暂未使用的资源

- `IconFolder` — 收录未用（decisions 收尾；用到时直接引用）
- `IconIphone` — 收录未用（设置页手机连接入口待接）

以下资源已收录但当前界面还没有引用；保留给对应的 E 组页面（用到时直接引用，不需要再改字体表）。

- `IconArchivebox`
- `IconArrowClockwise`
- `IconArrowUpCircleFill`
- `IconArrowUpRight`
- `IconBolt`
- `IconBoltFill`
- `IconCaptionsBubble`
- `IconChevronDown`
- `IconChevronUp`
- `IconCircle`
- `IconCircleFill`
- `IconDisplay`
- `IconDocRichtext`
- `IconExclamationmarkCircle`
- `IconExclamationmarkTriangleFill`
- `IconGlobe`
- `IconInfoCircle`
- `IconKey`
- `IconLock`
- `IconMagnifyingglass`
- `IconMinusCircle`
- `IconMoonZzz`
- `IconPersonFill`
- `IconPhoto`
- `IconPinFill`
- `IconPlus`
- `IconQrcode`
- `IconQuestionmarkBubble`
- `IconQuoteBubble`
- `IconRectangleOnRectangle`
- `IconRectangleTopthirdInsetFilled`
- `IconServerRack`
- `IconSidebarLeft`
- `IconSparkle`
- `IconSpeakerWave2`
- `IconStarFill`
- `IconTextMagnifyingglass`
- `IconTextQuote`
- `IconTextformatSizeLarger`
- `IconTextformatSizeSmaller`
- `IconTrash`
- `IconTrayAndArrowDown`
- `IconWaveform`
- `IconXmarkCircleFill`

## §6.6 提词岛图标（C5–C9）

| SF Symbol | 字形 | 资源 | Fluent 名称 |
|---|---|---|---|
| `bolt.fill` | `U+E61A` | `IconIslandBolt` | `ic_fluent_flash_24_regular` |
| `bubble.left` | `U+E3F3` | `IconIslandBubble` | `ic_fluent_comment_multiple_24_regular` |
| `chevron.left` | `U+F2AC` | `IconIslandChevronLeft` | `ic_fluent_chevron_left_24_regular` |
| `chevron.right` | `U+F2B2` | `IconIslandChevronRight` | `ic_fluent_chevron_right_24_regular` |
| `chevron.up` | `U+F2B8` | `IconIslandChevronUp` | `ic_fluent_chevron_up_24_regular` |
| `pencil.line` | `U+E5B2` | `IconIslandEdit` | `ic_fluent_edit_24_regular` |
| `eye.fill` | `U+EFC8` | `IconIslandEye` | `ic_fluent_eye_24_regular` |
| `textformat.size.smaller / larger` | `U+F0B9C` | `IconIslandFont` | `ic_fluent_text_font_size_24_regular` |
| `mic.fill` | `U+E7FB` | `IconIslandMic` | `ic_fluent_mic_24_regular` |
| `moon.zzz` | `U+EE45` | `IconIslandMoon` | `ic_fluent_weather_moon_24_regular` |
| `pause.fill` | `U+E8CD` | `IconIslandPause` | `ic_fluent_pause_24_regular` |
| `pin.fill / pin` | `U+E982` | `IconIslandPin` | `ic_fluent_pin_24_regular` |
| `play.fill` | `U+E991` | `IconIslandPlay` | `ic_fluent_play_24_regular` |
| `stop.fill` | `U+F0544` | `IconIslandStop` | `ic_fluent_stop_24_regular` |
| `waveform` | `U+E9DE` | `IconIslandWaveform` | `ic_fluent_pulse_24_regular` |
| `macwindow` | `U+EE5F` | `IconIslandWindow` | `ic_fluent_window_24_regular` |

## E29 回答浮窗图标（§6.7）

| SF Symbol | 字形 | 资源 | Fluent 名称 |
|---|---|---|---|
| `cursorarrow` | `U+F05FC` | `IconIslandCursor` | `ic_fluent_cursor_24_regular` |
| `cursorarrow.slash` | `U+F62F` | `IconIslandCursorOff` | `ic_fluent_prohibited_24_regular`（近似） |
| `ellipsis` | `U+E826` | `IconIslandEllipsis` | `ic_fluent_more_horizontal_24_regular` |

## §3.3 live 会中页暂停/继续（决议 live 1）

会中页顶栏 `PauseResumeButton` 用 24 px 的字形（岛上的 `IconIslandPause/Play` 是 28 px 字号下的同一符号，
会中页单独取 24 px 版本）：

| SF Symbol | 字形 | 资源 | Fluent 名称 |
|---|---|---|---|
| `pause.fill` | `U+F5A2` | `IconPause` | `ic_fluent_pause_24_regular` |
| `play.fill` | `U+F606` | `IconPlay` | `ic_fluent_play_24_regular` |
