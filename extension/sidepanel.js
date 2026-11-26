(function () {
  // Keep a live port so the service worker knows the panel is open (for Alt+P toggle).
  chrome.runtime.connect({ name: "sidepanel" });

  const DEFAULT_API_BASE = "https://localhost:450/HardwareStation";
  const barcodeTypes = ["Item", "Customer", "DataEntry", "Employee", "Salesperson", "DiscountCode", "GiftCard", "LoyaltyCard"];
  const loyaltyTypes = ["Loyalty", "Gift"];
  const paymentTypes = ["Visa", "Master"];
  const months = ["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"];

  let apiBase = DEFAULT_API_BASE;
  let state = null;
  let busy = false;
  let loyaltyUiIndex = 0;
  let paymentUiIndex = 0;
  let barcodeEdit = false;
  let cardEdit = false;
  let cardFormKind = "pay";

  const $ = (id) => document.getElementById(id);
  const statusEl = $("link-status");
  const apiLabel = $("api-label");

  ["1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "⌫", "OK"].forEach((label) => {
    const b = document.createElement("button");
    b.type = "button";
    b.textContent = label;
    b.dataset.pin = label === "C" ? "clear" : label === "⌫" ? "back" : label === "OK" ? "enter" : label;
    $("pin-keys").appendChild(b);
  });

  function isLegacyApiBase(base) {
    const b = String(base || "").trim();
    if (!b) return true;
    return /:446\/SIM\/?$/i.test(b) || /\/SIM\/?$/i.test(b) || /localhost:5058\/?$/i.test(b) || /127\.0\.0\.1:5058\/?$/i.test(b);
  }

  function normalizeBase(base) {
    const cleaned = String(base || DEFAULT_API_BASE).trim().replace(/\/+$/, "");
    return isLegacyApiBase(cleaned) ? DEFAULT_API_BASE : cleaned;
  }

  function apiUrl(path) {
    const p = String(path || "").replace(/^\//, "");
    return `${apiBase}/${p}`;
  }

  async function api(path, payload) {
    // Hardware Station: POST api/state, api/ui, api/station (action/route in body).
    let urlPath = String(path || "").replace(/^\//, "");
    let body = payload === undefined ? {} : (payload || {});
    const uiMatch = /^api\/ui\/(.+)$/i.exec(urlPath);
    if (uiMatch) {
      urlPath = "api/ui";
      body = Object.assign({ action: uiMatch[1] }, body);
    }
    const res = await fetch(apiUrl(urlPath), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || data.Message || data.LocalizedMessage || res.statusText || String(res.status));
    return data;
  }

  async function station(route, payload) {
    const res = await fetch(apiUrl("api/station"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(Object.assign({ route: route }, payload || {}))
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || data.Message || data.LocalizedMessage || res.statusText || String(res.status));
    return typeof data === "string" ? JSON.parse(data) : data;
  }

  function isLoyalty(c) { return loyaltyTypes.includes(c && c.type); }
  function isPayment(c) { return paymentTypes.includes(c && c.type); }
  function cardEntries(pred) {
    return (state && state.cards || []).map((c, i) => ({ c, i })).filter((x) => pred(x.c));
  }

  function years() {
    const y0 = new Date().getFullYear();
    return Array.from({ length: 7 }, (_, i) => y0 + i);
  }

  function setStatus(text, ok) {
    statusEl.textContent = text;
    statusEl.className = "status " + (ok === true ? "ok" : ok === false ? "bad" : "");
  }

  function fillCardSelect(sel, entries, prefer) {
    if (!sel) return;
    const keep = prefer != null ? String(prefer) : sel.value;
    sel.innerHTML = entries.map((e) =>
      `<option value="${e.i}">${e.c.number || "(empty)"} (${e.c.type})</option>`).join("");
    if (entries.length) {
      const ok = entries.some((e) => String(e.i) === keep);
      sel.value = ok ? keep : String(entries[0].i);
    }
  }

  function paint(s) {
    state = s;
    setStatus("Linked · " + s.revision, true);

    $("line-display").textContent = s.lineDisplay || "—";
    const lineEdit = $("line-edit");
    if (document.activeElement !== lineEdit)
      lineEdit.value = (s.lineDisplay || "").split(/\r?\n/)[0] || "";

    const light = s.light || {};
    const on = !!light.isOn;
    const bulbColor = light.color || "#d0d0d0";
    ["lane-light", "lane-light-2"].forEach((id) => {
      const bulb = $(id);
      if (!bulb) return;
      bulb.dataset.on = on ? "true" : "false";
      bulb.style.setProperty("--bulb", on ? bulbColor : "#d0d0d0");
      if (light.opacity != null && on) bulb.style.opacity = String(light.opacity);
      else bulb.style.opacity = "";
    });
    $("light-status").textContent = `${on ? "ON" : "OFF"} · ${light.status || ""} · ${light.detail || ""}`;

    $("btn-drawer-main").dataset.open = s.mainDrawerOpen ? "true" : "false";
    $("btn-drawer-sec").dataset.open = s.secondaryDrawerOpen ? "true" : "false";
    $("btn-drawer-main").textContent = s.mainDrawerOpen ? "Drawer 1 · open" : "Drawer 1";
    $("btn-drawer-sec").textContent = s.secondaryDrawerOpen ? "Drawer 2 · open" : "Drawer 2";

    const bSel = $("barcode-pick").value;
    $("barcode-pick").innerHTML = (s.barcodes || []).map((b, i) =>
      `<option value="${i}">${b.number} (${b.type})</option>`).join("");
    if ((s.barcodes || []).length) {
      const idx = s.barcodeIndex != null ? s.barcodeIndex : bSel;
      $("barcode-pick").value = String(Math.min(+idx || 0, s.barcodes.length - 1));
    }
    $("last-barcode").textContent = s.lastBarcode ? `Last · ${s.lastBarcode}` : "—";

    const kg = ((s.scaleGrams || 0) / 1000).toFixed(3);
    $("scale-value").textContent = kg;
    if (document.activeElement !== $("scale-grams"))
      $("scale-grams").value = String(s.scaleGrams != null ? s.scaleGrams : 1000);
    const scalePick = $("scale-pick");
    const sSel = scalePick.value;
    scalePick.innerHTML = (s.scales || []).map((sc, i) =>
      `<option value="${i}">${sc.name || "Scale"} (${sc.unit || ""})</option>`).join("");
    if ((s.scales || []).length) {
      const idx = s.scaleIndex != null ? s.scaleIndex : sSel;
      scalePick.value = String(Math.min(+idx || 0, s.scales.length - 1));
    }

    const loyalty = cardEntries(isLoyalty);
    const payment = cardEntries(isPayment);
    const prefer = s.cardIndex != null ? s.cardIndex : null;
    fillCardSelect($("loyalty-pick"), loyalty,
      loyalty.some((e) => e.i === loyaltyUiIndex) ? loyaltyUiIndex
        : (prefer != null && loyalty.some((e) => e.i === prefer) ? prefer : (loyalty[0] && loyalty[0].i)));
    fillCardSelect($("card-pick"), payment,
      payment.some((e) => e.i === paymentUiIndex) ? paymentUiIndex
        : (prefer != null && payment.some((e) => e.i === prefer) ? prefer : (payment[0] && payment[0].i)));
    if ($("loyalty-pick").value !== "") loyaltyUiIndex = +$("loyalty-pick").value;
    if ($("card-pick").value !== "") paymentUiIndex = +$("card-pick").value;

    const pinLen = (s.pinValue || "").length;
    const stars = pinLen ? "*".repeat(Math.min(pinLen, 6)) : "—";
    $("pin-lcd").textContent = `${stars} · ${Number(s.pinAmount || 0).toFixed(2)}`;
    if (document.activeElement !== $("pin-amount"))
      $("pin-amount").value = Number(s.pinAmount || 0).toFixed(2);

    $("receipt-text").textContent = (s.receiptLines || []).join("\n") || "";

    $("wedge-enabled").checked = !!s.keyboardWedge;
    if (document.activeElement !== $("wedge-delay"))
      $("wedge-delay").value = String(s.wedgeDelaySeconds != null ? s.wedgeDelaySeconds : 2);

    $("coin-status").textContent = `Last · ${s.coinChange || 0}`;
    const notes = s.orderNotifications || [];
    $("order-status").textContent = notes.length ? notes[0].text : (s.mpos && s.mpos.orderMessage) || "—";

    const m = s.mpos || {};
    $("pay-status").textContent =
      `${s.paymentState || "—"} · ${m.lastPaymentOperation || ""} · ${m.isApproved ? "OK" : ""}`.trim();
  }

  async function refresh() {
    try {
      const s = await api("api/state");
      if (!state || s.revision !== state.revision) paint(s);
      else setStatus("Linked · " + s.revision, true);
    } catch (e) {
      setStatus("API offline · " + (e.message || e), false);
    }
  }

  async function uiAction(action, payload) {
    if (busy) return;
    busy = true;
    try { paint(await api("api/ui/" + action, payload || {})); }
    catch (e) { setStatus(e.message, false); }
    finally { busy = false; }
  }

  function closeSetups() {
    $("barcode-setup").hidden = true;
    $("barcode-setup").innerHTML = "";
    $("card-setup").hidden = true;
    $("card-setup").innerHTML = "";
  }

  function showBarcodeForm(edit) {
    closeSetups();
    barcodeEdit = !!edit;
    const b = edit && state && state.barcodes[state.barcodeIndex]
      ? state.barcodes[state.barcodeIndex]
      : { number: "", type: "Item", label: "Code128" };
    const box = $("barcode-setup");
    box.hidden = false;
    box.innerHTML = `
      <h3>${edit ? "Edit" : "Add"} barcode</h3>
      <form id="barcode-form">
        <input name="number" value="${b.number || ""}" placeholder="Barcode" required />
        <select name="type">${barcodeTypes.map((t) => `<option ${t === b.type ? "selected" : ""}>${t}</option>`).join("")}</select>
        <select name="label"><option>Code128</option></select>
        <div class="row">
          <button type="submit" class="act">Save</button>
          <button type="button" class="ghost" id="bc-cancel">Back</button>
        </div>
      </form>`;
    $("bc-cancel").onclick = () => closeSetups();
    $("barcode-form").onsubmit = async (e) => {
      e.preventDefault();
      if (busy) return;
      busy = true;
      try {
        const f = new FormData(e.target);
        paint(await api("api/ui/saveBarcode", {
          number: f.get("number"),
          type: f.get("type"),
          label: f.get("label"),
          editIndex: barcodeEdit ? String(state.barcodeIndex) : ""
        }));
        closeSetups();
      } catch (err) { setStatus(err.message, false); }
      finally { busy = false; }
    };
  }

  function showCardForm(edit, kind) {
    closeSetups();
    cardEdit = !!edit;
    cardFormKind = kind === "loyalty" ? "loyalty" : "pay";
    const types = cardFormKind === "loyalty" ? loyaltyTypes : paymentTypes;
    const entries = cardEntries(cardFormKind === "loyalty" ? isLoyalty : isPayment);
    const pick = cardFormKind === "loyalty" ? $("loyalty-pick") : $("card-pick");
    const selectedGlobal = pick && pick.value !== "" ? +pick.value : (entries[0] && entries[0].i);
    const c = edit && state && state.cards[selectedGlobal]
      ? state.cards[selectedGlobal]
      : { number: "", type: types[0], month: "01", year: new Date().getFullYear() + 1, track: "" };
    const box = $("card-setup");
    box.hidden = false;
    box.innerHTML = `
      <h3>${edit ? "Edit" : "Add"} ${cardFormKind === "loyalty" ? "loyalty" : "card"}</h3>
      <form id="card-form">
        <input name="number" value="${c.number || ""}" placeholder="Number" />
        <select name="type">${types.map((t) => `<option ${t === c.type ? "selected" : ""}>${t}</option>`).join("")}</select>
        <select name="month">${months.map((m) => `<option ${m === (c.month || "01") ? "selected" : ""}>${m}</option>`).join("")}</select>
        <select name="year">${years().map((y) => `<option ${y === Number(c.year) ? "selected" : ""}>${y}</option>`).join("")}</select>
        <input name="track" value="${c.track || ""}" placeholder="Track (optional)" />
        <div class="row">
          <button type="submit" class="act">Save</button>
          <button type="button" class="ghost" id="card-cancel">Back</button>
        </div>
      </form>`;
    $("card-cancel").onclick = () => closeSetups();
    $("card-form").onsubmit = async (e) => {
      e.preventDefault();
      if (busy) return;
      busy = true;
      try {
        const f = new FormData(e.target);
        paint(await api("api/ui/saveCard", {
          mode: "number",
          number: f.get("number"),
          type: f.get("type"),
          month: f.get("month"),
          year: f.get("year"),
          track: f.get("track"),
          editIndex: cardEdit ? String(selectedGlobal) : ""
        }));
        closeSetups();
      } catch (err) { setStatus(err.message, false); }
      finally { busy = false; }
    };
  }

  async function swipe(kind) {
    const pick = kind === "msr" ? $("loyalty-pick") : $("card-pick");
    if (busy || !state || !pick || pick.value === "") return;
    busy = true;
    try {
      await api("api/ui/selectCard", { index: String(pick.value) });
      paint(await api("api/ui/swipe", { channel: kind === "pay" ? "payment" : "msr" }));
    } catch (e) { setStatus(e.message, false); }
    finally { busy = false; }
  }

  function applyTheme(theme) {
    const next = theme === "light" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", next);
    const toggle = $("theme-toggle");
    const label = $("theme-label");
    if (toggle) toggle.checked = next === "light";
    if (label) label.textContent = next === "light" ? "Light" : "Dark";
  }

  $("theme-toggle").onchange = () => {
    const next = $("theme-toggle").checked ? "light" : "dark";
    applyTheme(next);
    chrome.storage.sync.set({ theme: next });
  };
  $("btn-options").onclick = () => chrome.runtime.openOptionsPage();
  $("btn-refresh").onclick = () => refresh();

  $("btn-line-set").onclick = () => uiAction("lineDisplay", { text: $("line-edit").value || "" });
  $("btn-line-clear").onclick = () => uiAction("clearLineDisplay", {});
  $("btn-light-on").onclick = () => uiAction("light", {
    action: "SwitchOn", number: "1", blinkOn: "0", blinkOff: "0",
    color: String($("light-color").value || "262144"), alarm: "1"
  });
  $("btn-light-off").onclick = () => uiAction("light", { action: "SwitchOff", number: "1" });
  $("btn-drawer-main").onclick = () => uiAction("drawer", { drawer: "main" });
  $("btn-drawer-sec").onclick = () => uiAction("drawer", { drawer: "secondary" });

  $("barcode-pick").onchange = () => uiAction("selectBarcode", { index: String($("barcode-pick").value || 0) });
  $("btn-scan").onclick = async () => {
    if (busy || !state) return;
    busy = true;
    try {
      await api("api/ui/selectBarcode", { index: String($("barcode-pick").value || 0) });
      paint(await api("api/ui/scan", {}));
    } catch (e) { setStatus(e.message, false); }
    finally { busy = false; }
  };
  $("btn-bc-add").onclick = () => showBarcodeForm(false);
  $("btn-bc-edit").onclick = () => {
    if (!state || !(state.barcodes || []).length) { showBarcodeForm(false); return; }
    showBarcodeForm(true);
  };

  $("btn-scale-set").onclick = () => uiAction("scaleWeight", { grams: String($("scale-grams").value || "0") });
  $("btn-scale-zero").onclick = () => uiAction("scaleWeight", { grams: "0" });
  $("scale-pick").onchange = () => uiAction("selectScale", { index: String($("scale-pick").value || "0") });

  $("loyalty-pick").onchange = async () => {
    if (!state || busy || $("loyalty-pick").value === "") return;
    loyaltyUiIndex = +$("loyalty-pick").value;
    await uiAction("selectCard", { index: String($("loyalty-pick").value) });
  };
  $("card-pick").onchange = async () => {
    if (!state || busy || $("card-pick").value === "") return;
    paymentUiIndex = +$("card-pick").value;
    await uiAction("selectCard", { index: String($("card-pick").value) });
  };
  $("btn-msr-swipe").onclick = () => swipe("msr");
  $("btn-pay-swipe").onclick = () => swipe("pay");
  $("btn-loyalty-add").onclick = () => showCardForm(false, "loyalty");
  $("btn-loyalty-edit").onclick = () => {
    if (!cardEntries(isLoyalty).length) { showCardForm(false, "loyalty"); return; }
    showCardForm(true, "loyalty");
  };
  $("btn-card-add").onclick = () => showCardForm(false, "pay");
  $("btn-card-edit").onclick = () => {
    if (!cardEntries(isPayment).length) { showCardForm(false, "pay"); return; }
    showCardForm(true, "pay");
  };

  $("pin-keys").onclick = async (ev) => {
    const key = ev.target && ev.target.dataset && ev.target.dataset.pin;
    if (!key || busy) return;
    busy = true;
    try {
      await api("api/ui/pinAmount", { amount: String($("pin-amount").value || "0") });
      paint(await api("api/ui/pin", { key }));
    } catch (e) { setStatus(e.message, false); }
    finally { busy = false; }
  };

  $("btn-print").onclick = () => uiAction("print", { text: $("print-text").value || "" });
  $("btn-print-clear").onclick = () => uiAction("clearReceipt", {});
  $("btn-coin-dispense").onclick = () => uiAction("coinDispense", { amount: String($("coin-amount").value || "0") });
  $("btn-coin-test").onclick = () => uiAction("coinTest", {});
  $("btn-order-send").onclick = () => uiAction("order", { message: $("order-message").value || "" });
  $("btn-order-clear").onclick = () => uiAction("clearOrder", {});
  $("btn-wedge-save").onclick = () => uiAction("wedge", {
    enabled: $("wedge-enabled").checked ? "true" : "false",
    delay: String($("wedge-delay").value || "0")
  });

  document.querySelectorAll("[data-pay-op]").forEach((btn) => {
    btn.onclick = async () => {
      if (busy) return;
      busy = true;
      try {
        await station("PaymentTerminal/Lock", {});
        const result = await station("PaymentTerminal/" + btn.dataset.payOp, {});
        $("pay-status").textContent = JSON.stringify(result);
        paint(await api("api/state"));
      } catch (e) { setStatus(e.message, false); }
      finally { busy = false; }
    };
  });

  $("btn-station-run").onclick = async () => {
    if (busy) return;
    busy = true;
    try {
      const route = $("station-route").value;
      const payload = {};
      if (route === "LineDisplay/DisplayText") payload.lines = [$("line-edit").value || ""];
      if (route === "Printer/Print") payload.text = $("print-text").value || "STORE RECEIPT";
      if (route === "RemoteOrderDisplay/DisplayMessage") payload.message = $("order-message").value || "";
      if (route === "Light/SwitchOn") {
        payload.action = "SwitchOn";
        payload.number = "1";
        payload.blinkOn = "0";
        payload.blinkOff = "0";
        payload.color = String($("light-color").value || "262144");
        payload.alarm = "1";
      }
      if (route === "Light/SwitchOff") payload.number = "1";
      if (route === "CoinDispenser/DispenseChange") payload.amount = String($("coin-amount").value || "0");
      if (route === "CashDrawer/Open" || route === "CashDrawer/Close" || route === "CashDrawer/IsOpen") {
        payload.deviceName = "MockOPOSDrawer1";
      }
      const result = await station(route, payload);
      $("station-result").textContent = typeof result === "string" ? result : JSON.stringify(result, null, 2);
      paint(await api("api/state"));
    } catch (e) {
      $("station-result").textContent = e.message;
      setStatus(e.message, false);
    } finally { busy = false; }
  };

  (function wireSignature() {
    const canvas = $("sig-pad");
    const ctx = canvas.getContext("2d");
    let drawing = false;
    const pts = [];
    function pos(ev) {
      const r = canvas.getBoundingClientRect();
      const src = ev.touches ? ev.touches[0] : ev;
      return {
        x: (src.clientX - r.left) * (canvas.width / r.width),
        y: (src.clientY - r.top) * (canvas.height / r.height)
      };
    }
    function start(ev) { drawing = true; pts.push([pos(ev)]); ev.preventDefault(); }
    function move(ev) {
      if (!drawing) return;
      const p = pos(ev);
      const stroke = pts[pts.length - 1];
      stroke.push(p);
      ctx.strokeStyle = "#111";
      ctx.lineWidth = 2;
      ctx.lineCap = "round";
      if (stroke.length > 1) {
        const a = stroke[stroke.length - 2];
        ctx.beginPath();
        ctx.moveTo(a.x, a.y);
        ctx.lineTo(p.x, p.y);
        ctx.stroke();
      }
      ev.preventDefault();
    }
    function end() { drawing = false; }
    canvas.onmousedown = start;
    canvas.onmousemove = move;
    canvas.onmouseup = end;
    canvas.onmouseleave = end;
    canvas.ontouchstart = start;
    canvas.ontouchmove = move;
    canvas.ontouchend = end;
    $("btn-sig-clear").onclick = () => {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      pts.length = 0;
      uiAction("clearSignature", {});
    };
    $("btn-sig-ok").onclick = () => {
      const strokes = pts.map((stroke) => stroke.map((p) => `${Math.round(p.x)},${Math.round(p.y)}`).join(";")).join("|");
      uiAction("signature", { strokes });
    };
  })();

  chrome.storage.sync.get({ apiBase: DEFAULT_API_BASE, theme: "dark" }, (data) => {
    const raw = data.apiBase;
    const next = normalizeBase(raw);
    apiBase = next;
    apiLabel.textContent = apiBase;
    if (next !== String(raw || "").trim().replace(/\/+$/, "")) {
      chrome.storage.sync.set({ apiBase: next });
    }
    applyTheme(data.theme === "light" ? "light" : "dark");
    refresh();
    setInterval(refresh, 1500);
  });

  chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "sync") return;
    if (changes.apiBase) {
      apiBase = normalizeBase(changes.apiBase.newValue);
      apiLabel.textContent = apiBase;
      refresh();
    }
    if (changes.theme) applyTheme(changes.theme.newValue === "light" ? "light" : "dark");
  });
})();
