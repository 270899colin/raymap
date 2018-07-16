﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OpenSpace.Visual {
    /// <summary>
    /// Visual Material definition
    /// </summary>
    public class VisualMaterial {
        public List<TextureInfo> textures;
        public List<AnimatedTexture> animTextures;
        public uint flags;
        public List<int> textureTypes;
        public Pointer offset;
        public Vector4 ambientCoef;
        public Vector4 diffuseCoef;
        public Vector4 specularCoef;
        public Vector4 color;

        public Pointer off_animTextures_first;
        public Pointer off_animTextures_current;
        public ushort num_animTextures;

        public byte properties;
        private Material material;
        private Material materialBillboard;

        // UV scrolling
        public int currentAnimTexture = 0;
        public bool scrollingEnabled;
        public float scrollX, scrollY;

        // flags
        public static uint flags_isTransparent = (1 << 3);
        public static uint flags_backfaceCulling = (1 << 10);
        public static uint flags_isMaterialChromed = (1 << 22);

        //properties
        public static uint property_receiveShadows = 2;
        public static uint property_isSpriteGenerator = 4;
        public static uint property_isAnimatedSpriteGenerator = 12;
        public static uint property_isGrass = 0x2000;
        public static uint property_isWater = 0x1000;

        // TODO: Split material into material_main and material_light, find how these are stored differently.
        public Material Material {
            get {
                if (material == null) {
                    MapLoader l = MapLoader.Loader;
                    //bool backfaceCulling = ((flags & flags_backfaceCulling) == flags_backfaceCulling); // example: 4DDC43FF
                    bool useAlphaMask = false;
                    TextureInfo texMain = null, texSecondary = null;
                    if (textures != null && textures.Count > 0) {
                        texMain = textures[0];
                        if (textures.Count > 1) {
                            texSecondary = textures[1];
                        }
                    }
                    Material baseMaterial = l.baseMaterial;
                    bool transparent = IsTransparent;
                    if (textureTypes.Where(i => (i & 0x20) != 0).Count() > 0 || IsLight) {
                        baseMaterial = l.baseLightMaterial;
                    } else if (texMain != null && texSecondary != null) {
                        if (transparent) {
                            baseMaterial = l.baseBlendTransparentMaterial;
                            if (!texSecondary.Texture.HasColor()) useAlphaMask = true;
                        } /*else if (!texSecondary.texture.HasColor()) {
                            useAlphaMask = true;
                            baseMaterial = l.baseBlendTransparentMaterial;
                        }*/ else {
                            baseMaterial = l.baseBlendMaterial;
                        }
                    } else if (texMain != null && transparent) {
                        baseMaterial = l.baseTransparentMaterial;
                    }
                    //if (textureTypes.Where(i => ((i & 1) != 0)).Count() > 0) baseMaterial = loader.baseLightMaterial;
                    material = new Material(baseMaterial);
                    material.SetColor("_EmissionColor", new Color(ambientCoef.x / 2f, ambientCoef.y / 2f, ambientCoef.z / 2f, ambientCoef.w));
                    if (color.w > 0) {
                        material.SetColor("_Color", new Color(color.x, color.y, color.z, color.w));
                    } else {
                        material.SetColor("_Color", new Color(diffuseCoef.x, diffuseCoef.y, diffuseCoef.z, diffuseCoef.w));
                    }
                    if (texMain != null) material.SetTexture("_MainTex", texMain.Texture);
                    if (texSecondary != null) {
                        if (baseMaterial == l.baseBlendMaterial || baseMaterial == l.baseBlendTransparentMaterial) {
                            material.SetTexture("_MainTex2", texSecondary.Texture);
                            if (useAlphaMask) material.SetFloat("_UseAlpha", 1f);
                            //material.SetFloat("_Blend", 1f);
                        } else {
                            material.SetTexture("_DetailAlbedoMap", texSecondary.Texture);
                        }
                    }
                    if (texMain == null || texMain.Texture == null) {
                        // Don't want to see all those textureless planes, so create transparent texture and use that
                        Texture2D tex = new Texture2D(1, 1);
                        tex.SetPixel(0, 0, new Color(0, 0, 0, 0));
                        tex.Apply();
                        material.SetTexture("_MainTex", tex);
                    }
                }
                return material;
            }
        }

        public Material MaterialBillboard {
            get {
                if (materialBillboard == null) {
                    MapLoader l = MapLoader.Loader;
                    //bool backfaceCulling = ((flags & flags_backfaceCulling) == flags_backfaceCulling); // example: 4DDC43FF
                    bool useAlphaMask = false;
                    TextureInfo texMain = null, texSecondary = null;
                    if (textures != null && textures.Count > 0) {
                        texMain = textures[0];
                        if (textures.Count > 1) {
                            texSecondary = textures[1];
                        }
                    }
                    Material baseMaterial = l.billboardMaterial;
                    if (textureTypes.Where(i => (i & 0x20) != 0).Count() > 0 || IsLight) {
                        baseMaterial = l.billboardAdditiveMaterial;
                    }
                    bool transparent = IsTransparent;
                    materialBillboard = new Material(baseMaterial);
                    materialBillboard.SetColor("_EmissionColor", new Color(ambientCoef.x / 2f, ambientCoef.y / 2f, ambientCoef.z / 2f, ambientCoef.w));
                    if (color.w > 0) {
                        materialBillboard.SetColor("_Color", new Color(color.x, color.y, color.z, color.w));
                    } else {
                        materialBillboard.SetColor("_Color", new Color(diffuseCoef.x, diffuseCoef.y, diffuseCoef.z, diffuseCoef.w));
                    }
                    if (texMain != null) materialBillboard.SetTexture("_MainTex", texMain.Texture);
                    if (texSecondary != null) {
                        if (baseMaterial == l.baseBlendMaterial || baseMaterial == l.baseBlendTransparentMaterial) {
                            materialBillboard.SetTexture("_MainTex2", texSecondary.Texture);
                            if (useAlphaMask) materialBillboard.SetFloat("_UseAlpha", 1f);
                            //material.SetFloat("_Blend", 1f);
                        } else {
                            materialBillboard.SetTexture("_DetailAlbedoMap", texSecondary.Texture);
                        }
                    }
                    if (texMain == null || texMain.Texture == null) {
                        // Don't want to see all those textureless planes, so create transparent texture and use that
                        Texture2D tex = new Texture2D(1, 1);
                        tex.SetPixel(0, 0, new Color(0, 0, 0, 0));
                        tex.Apply();
                        material.SetTexture("_MainTex", tex);
                    }
                }
                return materialBillboard;
            }
        }

        public bool IsTransparent {
            get {
                //if (R3Loader.Loader.mode == R3Loader.Mode.Rayman2PC) R3Loader.Loader.print("Flags: " + flags + "Transparent flag: " + flags_isTransparent);
                if ((flags & flags_isTransparent) != 0 || MapLoader.Loader.mode == MapLoader.Mode.Rayman2PC) {
                    if (textures.Count > 0 && textures[0] != null) {
                        return textures[0].IsTransparent;
                    }
                    return false;
                } else return true;
            }
        }

        public bool IsLight {
            get {
                //if (R3Loader.Loader.mode == R3Loader.Mode.Rayman2PC) R3Loader.Loader.print("Flags: " + flags + "Transparent flag: " + flags_isTransparent);
                if ((flags & flags_isTransparent) != 0 || MapLoader.Loader.mode == MapLoader.Mode.Rayman2PC) {
                    if (textures.Count > 0 && textures[0] != null) {
                        return textures[0].IsLight;
                    }
                    return false;
                } else return true;
            }
        }

        public bool IsLockedAnimatedTexture {
            get { return (properties & 1) == 1; }
        }

        public VisualMaterial(Pointer offset) {
            this.offset = offset;
            textures = new List<TextureInfo>();
            textureTypes = new List<int>();
            animTextures = new List<AnimatedTexture>();
        }

        public static VisualMaterial Read(EndianBinaryReader reader, Pointer offset) {
            MapLoader l = MapLoader.Loader;
            VisualMaterial m = new VisualMaterial(offset);
            // Material struct = 0x188
            m.flags = reader.ReadUInt32(); // After this: 0x4
            m.ambientCoef  = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            m.diffuseCoef  = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            m.specularCoef = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            m.color        = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            reader.ReadUInt32(); // some specular parameter, 0x48
            
            List<Pointer> off_textures = new List<Pointer>();
            if (Settings.s.engineMode == Settings.EngineMode.R2) {
                Pointer off_texture = Pointer.Read(reader); // 0x4c
                //Pointer off_texture2 = Pointer.Read(reader);
                int type_texture = reader.ReadInt32(); // 0x50
                off_textures.Add(off_texture);
                m.textureTypes.Add(type_texture);

                reader.ReadInt32(); // 0x54
                float scrollX = reader.ReadSingle(); // 0x58
                float scrollY = reader.ReadSingle(); // 0x5c
                m.scrollingEnabled = reader.ReadUInt32()!=0; //0x60

                if (m.scrollingEnabled)
                {
                    m.scrollX = scrollX;
                    m.scrollY = scrollY;

                    MapLoader.Loader.print("Scrolling enabled, scrollX = " + m.scrollX + ", scrollY = " + m.scrollY);
                }

                reader.ReadInt32(); // 0x64
                m.off_animTextures_first = Pointer.Read(reader); // 0x68
                m.off_animTextures_current = Pointer.Read(reader); // 0x6c
                m.num_animTextures = reader.ReadUInt16();
                reader.ReadUInt16(); // 0x70
                reader.ReadUInt32(); // 0x74
                m.properties = reader.ReadByte();
                reader.ReadByte();
                reader.ReadByte();
                reader.ReadByte();
            } else {
                m.off_animTextures_first = Pointer.Read(reader);
                m.off_animTextures_current = Pointer.Read(reader);
                m.num_animTextures = reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadUInt32();
                reader.ReadByte();
                reader.ReadByte();
                m.properties = reader.ReadByte();
                reader.ReadByte();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                Pointer off_texture1 = Pointer.Read(reader);
                reader.ReadByte();
                reader.ReadByte();
                reader.ReadByte();
                byte scrollByte1 = reader.ReadByte();
                if ((scrollByte1 & 6) != 0) { // 6 = 110, so these two flags are for X and Y scrolling
                    m.scrollingEnabled = true;
                }
                int type_texture1 = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                m.scrollX = reader.ReadSingle();
                m.scrollY = reader.ReadSingle();
                reader.ReadBytes(0x2C);
                Pointer off_texture2 = Pointer.Read(reader);
                reader.ReadByte();
                reader.ReadByte();
                reader.ReadByte();
                byte scrollByte2 = reader.ReadByte();
                int type_texture2 = reader.ReadInt32();
                uint num_textures = 0;
                if (off_texture1 != null) {
                    off_textures.Add(off_texture1);
                    m.textureTypes.Add(type_texture1);
                }
                if (off_texture2 != null) {
                    off_textures.Add(off_texture2);
                    m.textureTypes.Add(type_texture2);
                }
                /*if (off_texture2 != null) num_textures++;
                R3Pointer[] off_textures = new R3Pointer[num_textures];
                int[] textureTypes = new int[num_textures];
                if (off_texture1 != null) {
                    off_textures[0] = off_texture1;
                    textureTypes[0] = type_texture1;
                }
                if (off_texture2 != null) {
                    off_textures[num_textures - 1] = off_texture2;
                    textureTypes[num_textures - 1] = type_texture2;
                }*/

                /*uint num_textures = Math.Min(reader.ReadUInt32(), 2);
                R3Pointer[] off_textures = new R3Pointer[num_textures];
                int[] textureTypes = new int[num_textures];
                for (uint i = 0; i < num_textures; i++) {
                    off_textures[i] = R3Pointer.Read(reader);
                    reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    textureTypes[i] = reader.ReadInt32();
                    if (num_textures > i + 1) reader.ReadBytes(0x3C);
                }*/
            }
            for (int i = 0; i < off_textures.Count; i++) {
                m.textures.Add(TextureInfo.FromOffset(off_textures[i]));
            }
            if (m.num_animTextures > 0 && m.off_animTextures_first != null) {
                Pointer off_currentAnimTexture = m.off_animTextures_first;
                Pointer.Goto(ref reader, m.off_animTextures_first);
                for (int i = 0; i < m.num_animTextures; i++) {
                    if (off_currentAnimTexture == m.off_animTextures_current) m.currentAnimTexture = i;
                    Pointer off_animTexture = Pointer.Read(reader);
                    float time = reader.ReadSingle();
                    m.animTextures.Add(new AnimatedTexture(off_animTexture, time));
                    Pointer off_nextAnimTexture = Pointer.Read(reader);
                    if (off_nextAnimTexture != null) {
                        off_currentAnimTexture = off_nextAnimTexture;
                        Pointer.Goto(ref reader, off_nextAnimTexture);
                    }
                }
            }

            return m;
        }

        public static VisualMaterial FromOffset(Pointer offset, bool createIfNull = false) {
            MapLoader l = MapLoader.Loader;
            for (int i = 0; i < l.materials.Length; i++) {
                if (offset == l.materials[i].offset) return l.materials[i];
            }
            if (createIfNull) {
                Array.Resize(ref l.materials, l.materials.Length + 1);
                l.materials[l.materials.Length - 1] = VisualMaterial.Read(offset.file.reader, offset);
                return l.materials[l.materials.Length - 1];
            }
            l.print("Material was null!");
            return null;
        }
    }
}
