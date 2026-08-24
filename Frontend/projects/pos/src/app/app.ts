import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'pos-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
