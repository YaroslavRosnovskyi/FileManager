function createFileInput() {
    const existingInput = document.getElementById('hidden-file-input');
    if (existingInput) {
        document.body.removeChild(existingInput);
    }

    const input = document.createElement('input');
    input.type = 'file';
    input.id = 'hidden-file-input';
    input.style.display = 'none';
    document.body.appendChild(input);
    return input;
}

function arrayBufferToBase64(buffer) {
    const uint8Array = new Uint8Array(buffer);
    let binary = '';
    const len = uint8Array.byteLength;
    
    const chunkSize = 8192;
    for (let i = 0; i < len; i += chunkSize) {
        const chunk = uint8Array.subarray(i, Math.min(i + chunkSize, len));
        binary += String.fromCharCode.apply(null, chunk);
    }
    
    return btoa(binary);
}

window.selectAndReadFile = function() {
    return new Promise((resolve) => {
        const input = createFileInput();
        
        input.onchange = async () => {
            console.log("File selected:", input.files);
            
            if (input.files.length > 0) {
                const file = input.files[0];
                console.log("Processing file:", file.name, "Size:", file.size);
                
                try {
                    const arrayBuffer = await new Promise((resolveBuffer) => {
                        const reader = new FileReader();
                        reader.onload = () => {
                            console.log("File read complete, buffer size:", reader.result.byteLength);
                            resolveBuffer(reader.result);
                        };
                        reader.onerror = (error) => {
                            console.error("Error reading file:", error);
                            resolveBuffer(null);
                        };
                        reader.readAsArrayBuffer(file);
                    });
                    
                    if (arrayBuffer) {
                        const uint8Array = new Uint8Array(arrayBuffer);
                        
                        
                        const byteArray = new Array(uint8Array.length);
                        for (let i = 0; i < uint8Array.length; i++) {
                            byteArray[i] = uint8Array[i];
                        }
                        
                        console.log("Created byte array of length:", byteArray.length);
                        
                        const result = {
                            name: file.name,
                            type: file.type,
                            size: file.size,
                            content: byteArray
                        };
                        
                        console.log("Returning file data, name:", result.name, "size:", result.size);
                        resolve(result);
                    } else {
                        console.error("Failed to read file, arrayBuffer is null");
                        resolve(null);
                    }
                } catch (error) {
                    console.error("Error in selectAndReadFile:", error);
                    resolve(null);
                }
            } else {
                console.log("No file selected");
                resolve(null);
            }
        };
        
        console.log("Opening file dialog");
        input.click();
    });
};

window.selectFileSimple = function() {
    return new Promise((resolve) => {
        const input = createFileInput();
        
        input.onchange = async () => {
            console.log("File selected (simple approach):", input.files);
            
            if (input.files.length > 0) {
                const file = input.files[0];
                console.log("File selected:", file.name, "Size:", file.size, "Type:", file.type);
                
                resolve({
                    name: file.name,
                    type: file.type,
                    size: file.size
                });
            } else {
                console.log("No file selected");
                resolve(null);
            }
        };
        
        console.log("Opening file dialog (simple approach)");
        input.click();
    });
};

window.readSelectedFileContent = function() {
    return new Promise((resolve, reject) => {
        const input = document.getElementById('hidden-file-input');
        
        if (!input || !input.files || input.files.length === 0) {
            console.error("No file selected in input element");
            reject("No file selected");
            return;
        }
        
        const file = input.files[0];
        console.log("Reading file content:", file.name);
        
        const reader = new FileReader();
        
        reader.onload = () => {
            console.log("File read complete, array length:", new Uint8Array(reader.result).length);
            resolve(new Uint8Array(reader.result));
        };
        
        reader.onerror = (error) => {
            console.error("Error reading file:", error);
            reject(error);
        };
        
        reader.readAsArrayBuffer(file);
    });
};

window.saveFile = function(filename, contentType, content) {
    const blob = new Blob([content], { type: contentType });
    
    const url = URL.createObjectURL(blob);
    
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.style.display = 'none';
    
    document.body.appendChild(a);
    a.click();
    
    setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }, 100);
    
    return true;
};

window.downloadFileViaDrag = async function(fileName, fileId, dotNetReference) {
    console.log(`[DragOut] Starting drag-out download for: ${fileName}`);
    
    try {
        const result = await dotNetReference.invokeMethodAsync('GetFileForDragOut', fileId);
        if (!result || !result.success) {
            console.error(`[DragOut] Failed to get file content for: ${fileName}`);
            return false;
        }
        
        console.log(`[DragOut] Got file content, size: ${result.content.length} bytes`);
        
        const blob = new Blob([new Uint8Array(result.content)], { type: result.contentType });
        
        const url = URL.createObjectURL(blob);
        
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.style.display = 'none';
        
        document.body.appendChild(a);
        a.click();
        
        setTimeout(() => {
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }, 100);
        
        console.log(`[DragOut] Successfully downloaded: ${fileName}`);
        return true;
        
    } catch (error) {
        console.error(`[DragOut] Error downloading file: ${fileName}`, error);
        return false;
    }
};

window.enableFileDragOut = function(fileRowId, fileName, fileId, dotNetReference) {
    console.log(`[DragOut] Enabling drag-out for: ${fileName} (${fileRowId})`);
    
    const fileRow = document.getElementById(fileRowId);
    if (!fileRow) {
        console.error(`[DragOut] File row not found: ${fileRowId}`);
        return false;
    }
    
    fileRow.draggable = true;
    
    fileRow.dataset.fileName = fileName;
    fileRow.dataset.fileId = fileId;
    
    fileRow.addEventListener('dragstart', async (e) => {
        console.log(`[DragOut] Drag started for: ${fileName}`);
        
        try {
            e.dataTransfer.effectAllowed = 'copy';
            
            if ('setDragImage' in e.dataTransfer) {
                const dragImage = document.createElement('div');
                dragImage.style.padding = '10px';
                dragImage.style.backgroundColor = '#007bff';
                dragImage.style.color = 'white';
                dragImage.style.borderRadius = '4px';
                dragImage.style.fontSize = '14px';
                dragImage.style.position = 'absolute';
                dragImage.style.top = '-1000px';
                dragImage.textContent = `?? ${fileName}`;
                document.body.appendChild(dragImage);
                e.dataTransfer.setDragImage(dragImage, 0, 0);
                
                setTimeout(() => {
                    if (document.body.contains(dragImage)) {
                        document.body.removeChild(dragImage);
                    }
                }, 100);
            }
            
            e.dataTransfer.setData('text/plain', fileName);
            e.dataTransfer.setData('application/octet-stream', fileId);
            
            if ('setData' in e.dataTransfer) {
                try {
                    const result = await dotNetReference.invokeMethodAsync('GetFileForDragOut', fileId);
                    if (result && result.success) {
                        const blob = new Blob([new Uint8Array(result.content)], { type: result.contentType });
                        const url = URL.createObjectURL(blob);
                        
                        e.dataTransfer.setData('DownloadURL', `${result.contentType}:${fileName}:${url}`);
                        
                        console.log(`[DragOut] Set DownloadURL for: ${fileName}`);
                        
                        setTimeout(() => {
                            URL.revokeObjectURL(url);
                        }, 5000);
                    }
                } catch (error) {
                    console.error(`[DragOut] Error setting DownloadURL for: ${fileName}`, error);
                }
            }
            
        } catch (error) {
            console.error(`[DragOut] Error in dragstart for: ${fileName}`, error);
        }
    });
    
    fileRow.addEventListener('dragend', (e) => {
        console.log(`[DragOut] Drag ended for: ${fileName}`);
    });
    
    fileRow.addEventListener('dragover', (e) => {
        e.preventDefault();
    });
    
    console.log(`[DragOut] Drag-out enabled for: ${fileName}`);
    return true;
};

window.enableAllFilesDragOut = function(dotNetReference) {
    console.log(`[DragOut] Enabling drag-out for all file rows`);
    
    const fileRows = document.querySelectorAll('[data-file-row]');
    let count = 0;
    
    fileRows.forEach(row => {
        const fileName = row.dataset.fileName;
        const fileId = row.dataset.fileId;
        
        if (fileName && fileId && row.id) {
            if (enableFileDragOut(row.id, fileName, fileId, dotNetReference)) {
                count++;
            }
        }
    });
    
    console.log(`[DragOut] Enabled drag-out for ${count} files`);
    return count;
};

let isProcessingDrop = false;

window.initializeDragAndDrop = function(dropZoneId, dotNetReference) {
    console.log(`[DragDrop] Initializing for ${dropZoneId}...`);
    
    const dropZone = document.getElementById(dropZoneId);
    if (!dropZone) {
        console.error(`[DragDrop] Drop zone element not found: ${dropZoneId}`);
        return false;
    }
    
    if (dropZone._dragDropInitialized) {
        console.log(`[DragDrop] Removing previous event listeners`);
        document.removeEventListener('dragover', preventDefaultDragOver, true);
        document.removeEventListener('drop', preventDefaultDrop, true);
    }
    
    function preventDefaultDragOver(e) {
        e.preventDefault();
    }
    
    function preventDefaultDrop(e) {
        console.log('[DragDrop] Preventing default drop on document');
        e.preventDefault();
    }
    
    document.addEventListener('dragover', preventDefaultDragOver, true);
    document.addEventListener('drop', preventDefaultDrop, true);
    
    function handleDragEnter(e) {
        console.log('[DragDrop] Drag enter');
        e.preventDefault();
        e.stopPropagation();
        
        if (e.dataTransfer.types.includes('Files')) {
            dropZone.classList.add('drag-over');
        }
    }
    
    function handleDragOver(e) {
        e.preventDefault();
        e.stopPropagation();
        
        e.dataTransfer.dropEffect = 'copy';
        
        if (e.dataTransfer.types.includes('Files') && !dropZone.classList.contains('drag-over')) {
            dropZone.classList.add('drag-over');
        }
    }
    
    function handleDragLeave(e) {
        console.log('[DragDrop] Drag leave');
        e.preventDefault();
        e.stopPropagation();
        
        const rect = dropZone.getBoundingClientRect();
        const x = e.clientX;
        const y = e.clientY;
        
        if (x <= rect.left || x >= rect.right || y <= rect.top || y >= rect.bottom) {
            dropZone.classList.remove('drag-over');
        }
    }
    
    async function handleDrop(e) {
        console.log('[DragDrop] Drop event triggered');
        e.preventDefault();
        e.stopPropagation();
        
        dropZone.classList.remove('drag-over');
        
        if (isProcessingDrop) {
            console.log('[DragDrop] Already processing a drop operation, ignoring this one');
            return;
        }
        
        isProcessingDrop = true;
        
        try {
            const dt = e.dataTransfer;
            if (!dt.files || dt.files.length === 0) {
                console.log('[DragDrop] No files in drop event');
                isProcessingDrop = false;
                return;
            }
            
            console.log(`[DragDrop] Files dropped: ${dt.files.length}`);
            
            for (let i = 0; i < dt.files.length; i++) {
                const file = dt.files[i];
                console.log(`[DragDrop] Processing file: ${file.name}, Size: ${file.size}, Type: ${file.type}`);
                
                let uploadId = null;
                try {
                    const fileInfo = {
                        name: file.name,
                        type: file.type,
                        size: file.size
                    };
                    
                    uploadId = await dotNetReference.invokeMethodAsync('StartFileUpload', fileInfo);
                    if (!uploadId) {
                        console.error(`[DragDrop] Failed to start upload for file: ${file.name}`);
                        continue;
                    }
                    
                    console.log(`[DragDrop] Started upload with ID: ${uploadId} for file: ${file.name}`);
                    
                    const CHUNK_SIZE = 1024 * 32;
                    let offset = 0;
                    
                    async function readAndSendChunk() {
                        if (offset >= file.size) {
                            const success = await dotNetReference.invokeMethodAsync('FinishFileUpload', uploadId);
                            console.log(`[DragDrop] Finished upload for file: ${file.name}, success: ${success}`);
                            return;
                        }
                        
                        const chunk = file.slice(offset, offset + CHUNK_SIZE);
                        
                        const arrayBuffer = await new Promise((resolve, reject) => {
                            const reader = new FileReader();
                            reader.onload = e => resolve(e.target.result);
                            reader.onerror = e => reject(e);
                            reader.readAsArrayBuffer(chunk);
                        });
                        
                        const base64String = arrayBufferToBase64(arrayBuffer);
                        console.log(`[DragDrop] Converting chunk at offset ${offset}, size: ${arrayBuffer.byteLength}, base64 length: ${base64String.length}`);
                        
                        const progress = await dotNetReference.invokeMethodAsync('UploadFileChunk', uploadId, base64String, offset, file.size);
                        console.log(`[DragDrop] Sent chunk at offset ${offset} for file: ${file.name}, progress: ${progress}%`);
                        
                        offset += chunk.size;
                        
                        setTimeout(readAndSendChunk, 10);
                    }
                    
                    await readAndSendChunk();
                    
                } catch (error) {
                    console.error(`[DragDrop] Error processing dropped file: ${file.name}`, error);
                    if (uploadId) {
                        try {
                            await dotNetReference.invokeMethodAsync('CancelFileUpload', uploadId);
                        } catch (cancelError) {
                            console.error(`[DragDrop] Error canceling upload: ${cancelError}`);
                        }
                    }
                }
            }
        } finally {
            isProcessingDrop = false;
        }
    }
    
    dropZone.removeEventListener('dragenter', handleDragEnter);
    dropZone.removeEventListener('dragover', handleDragOver);
    dropZone.removeEventListener('dragleave', handleDragLeave);
    dropZone.removeEventListener('drop', handleDrop);
    
    dropZone.addEventListener('dragenter', handleDragEnter);
    dropZone.addEventListener('dragover', handleDragOver);
    dropZone.addEventListener('dragleave', handleDragLeave);
    dropZone.addEventListener('drop', handleDrop);
    
    dropZone.addEventListener('click', (e) => {
        if (e.target.tagName === 'BUTTON' || 
            e.target.closest('button') || 
            e.target.tagName === 'A' || 
            e.target.closest('a')) {
            return;
        }
        
        if (isProcessingDrop) {
            console.log('[DragDrop] Already processing files, ignoring click');
            return;
        }
        
        console.log('[DragDrop] Drop zone clicked, opening file dialog');
        
        const input = createFileInput();
        input.multiple = true;
        
        input.onchange = async () => {
            if (!input.files || input.files.length === 0) {
                console.log('[DragDrop] No files selected');
                return;
            }
            
            isProcessingDrop = true;
            
            try {
                console.log(`[DragDrop] Files selected via click: ${input.files.length}`);
                
                for (let i = 0; i < input.files.length; i++) {
                    const file = input.files[i];
                    console.log(`[DragDrop] Processing clicked file: ${file.name}, Size: ${file.size}`);
                    
                    let uploadId = null;
                    try {
                        const fileInfo = {
                            name: file.name,
                            type: file.type,
                            size: file.size
                        };
                        
                        uploadId = await dotNetReference.invokeMethodAsync('StartFileUpload', fileInfo);
                        if (!uploadId) {
                            console.error(`[DragDrop] Failed to start upload for file: ${file.name}`);
                            continue;
                        }
                        
                        console.log(`[DragDrop] Started upload with ID: ${uploadId} for file: ${file.name}`);
                        
                        const CHUNK_SIZE = 1024 * 32;
                        let offset = 0;
                        
                        async function readAndSendChunk() {
                            if (offset >= file.size) {
                                const success = await dotNetReference.invokeMethodAsync('FinishFileUpload', uploadId);
                                console.log(`[DragDrop] Finished upload for file: ${file.name}, success: ${success}`);
                                return;
                            }
                            
                            const chunk = file.slice(offset, offset + CHUNK_SIZE);
                            
                            const arrayBuffer = await new Promise((resolve, reject) => {
                                const reader = new FileReader();
                                reader.onload = e => resolve(e.target.result);
                                reader.onerror = e => reject(e);
                                reader.readAsArrayBuffer(chunk);
                            });
                            

                            const base64String = arrayBufferToBase64(arrayBuffer);
                            console.log(`[DragDrop] Converting chunk at offset ${offset}, size: ${arrayBuffer.byteLength}, base64 length: ${base64String.length}`);
                            
                            const progress = await dotNetReference.invokeMethodAsync('UploadFileChunk', uploadId, base64String, offset, file.size);
                            console.log(`[DragDrop] Sent chunk at offset ${offset} for file: ${file.name}, progress: ${progress}%`);
                            
                            offset += chunk.size;
                            
                            setTimeout(readAndSendChunk, 10);
                        }
                        
                        await readAndSendChunk();
                        
                    } catch (error) {
                        console.error(`[DragDrop] Error processing clicked file: ${file.name}`, error);
                        if (uploadId) {
                            try {
                                await dotNetReference.invokeMethodAsync('CancelFileUpload', uploadId);
                            } catch (cancelError) {
                                console.error(`[DragDrop] Error canceling upload: ${cancelError}`);
                            }
                        }
                    }
                }
            } finally {
                isProcessingDrop = false;
            }
        };
        
        input.click();
    });
    
    dropZone._dragDropInitialized = true;
    
    console.log(`[DragDrop] Drag and drop initialized successfully for: ${dropZoneId}`);
    return true;
};

window.debugDragAndDrop = function(enable) {
    console.log(`[DragDrop] Debug mode ${enable ? 'enabled' : 'disabled'}`);
    
    const debugElement = document.getElementById('dragDebug');
    if (debugElement) {
        debugElement.style.display = enable ? 'block' : 'none';
    }
    
    if (enable) {
        const dropZone = document.getElementById('dropZone');
        if (dropZone) {
            dropZone.style.outline = '2px solid red';
            setTimeout(() => {
                dropZone.style.outline = '';
                
                dropZone.classList.add('drag-over');
                console.log('[DragDrop] Added drag-over class for testing');
                
                setTimeout(() => {
                    dropZone.classList.remove('drag-over');
                    console.log('[DragDrop] Removed drag-over class after test');
                    
                    if (debugElement) {
                        debugElement.textContent = 'CSS test complete. If you didn\'t see blue border, check CSS loading.';
                    }
                }, 2000);
            }, 500);
        }
    }
    
    return true;
};

window.testJavaScriptInterop = function() {
    console.log("JavaScript interop test function called successfully");
    return "JavaScript interop is working correctly!";
};

window.initializeGlobalClickHandler = function(dotNetReference) {
    console.log("Initializing global click handler for dropdowns");
    
    if (window.globalClickHandler) {
        document.removeEventListener('click', window.globalClickHandler);
    }
    
    window.globalClickHandler = function(event) {
        const dropdowns = document.querySelectorAll('.dropdown');
        let clickedInsideDropdown = false;
        
        dropdowns.forEach(dropdown => {
            if (dropdown.contains(event.target)) {
                clickedInsideDropdown = true;
            }
        });
        
        if (!clickedInsideDropdown) {
            try {
                dotNetReference.invokeMethodAsync('CloseAllDropdowns');
            } catch (error) {
                console.log("Could not invoke CloseAllDropdowns:", error);
            }
        }
    };
    
    document.addEventListener('click', window.globalClickHandler);
    
    return true;
};