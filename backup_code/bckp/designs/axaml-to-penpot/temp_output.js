// ============================================================================
// StartPage — Auto-generated AXAML → Penpot
// Canvas: 1280×800 | Shapes: 8
// Execute via MCP execute_code (type: script)
// ============================================================================

(function() {
  var root = storage.preparePage('Avalonia - StartPage');
  var board = storage.createBoard('StartPage', 1280, 800, '#0C0C0E', 1);
  root.appendChild(board);

  var s0 = storage.createRect('Border_bg', 0, 0, 1280, 800, '#0C0C0E', 1);
  board.appendChild(s0);

  var s1 = storage.createRect('Border_bg', 12, 12, 1256, 776, '#FFFFFF', 0.13, 12);
  board.appendChild(s1);

  var s2 = storage.createFromSvg('Path_2', '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><path d="M 12 2C 6.48 2 2 6.48 2 12C 2 17.52 6.48 22 12 22C 17.52 22 22 17.52 22 12C 22 6.48 17.52 2 12 2 Z M 10 16.5V 7.5L 16 12L 10 16.5 Z" fill="#E5E5E5" fill-opacity="1"/></svg>');
  s2.x = 576; s2.y = 48;
  board.appendChild(s2);

  var s3 = storage.createText('Text_3', 'Drag and Drop Files Here', 28, 700, '#E5E5E5', 1, 'center');
  s3.x = 0; s3.y = 204;
  storage.centerTextX(s3, 640);
  board.appendChild(s3);

  var s4 = storage.createRect('Button_bg', 0, 248, 320, 40, '#FFFFFF', 0.12, 20);
  board.appendChild(s4);

  var s5 = storage.createText('Button_text', 'Open...', 14, 600, '#E5E5E5', 1, 'center');
  s5.x = 0; s5.y = 261;
  storage.centerTextX(s5, 160);
  board.appendChild(s5);

  var s6 = storage.createRect('Button_bg', 0, 300, 320, 40, '#FFFFFF', 0.12, 20);
  board.appendChild(s6);

  var s7 = storage.createText('Button_text', 'Open Folder', 14, 600, '#E5E5E5', 1, 'center');
  s7.x = 0; s7.y = 313;
  storage.centerTextX(s7, 160);
  board.appendChild(s7);

  return 'StartPage: 8 shapes created';
})();

