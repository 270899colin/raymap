﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OpenSpace.Collide {
    public class CollideMeshElement : ICollideGeometricElement {
        [JsonIgnore]
        public CollideMeshObject mesh;
        [JsonIgnore]
        public Pointer offset;

        [JsonIgnore]
        public Pointer off_material;
        [JsonIgnore]
        public Pointer off_triangles; // num_triangles * 3 * 0x2
        [JsonIgnore]
        public Pointer off_mapping; // num_triangles * 3 * 0x2. Max: num_uvs-1
        [JsonIgnore]
        public Pointer off_normals; // num_triangles * 3 * 0x4. 1 normal per face, kinda logical for collision I guess
        [JsonIgnore]
        public Pointer off_uvs;
        public ushort num_triangles;
        public ushort num_mapping;
        [JsonIgnore]
        public Pointer off_unk;
        [JsonIgnore]
        public Pointer off_unk2;
        public ushort num_mapping_entries;

        public GameMaterial gameMaterial;
        public int[] triangles = null;
        public Vector3[] normals = null;
        public int[] mapping = null;
        public Vector2[] uvs = null;

        [JsonIgnore]
        private GameObject gao = null;
        [JsonIgnore]
        public GameObject Gao {
            get {
                if (gao == null) {
                    gao = new GameObject("Collide Submesh @ " + offset);// Create object and read triangle data
                    gao.layer = LayerMask.NameToLayer("Collide");
                    CreateUnityMesh();
                }
                return gao;
            }
        }

        public CollideMeshElement(Pointer offset, CollideMeshObject mesh) {
            this.mesh = mesh;
            this.offset = offset;
        }

        private void CreateUnityMesh() {
            if(num_triangles > 0) {
                Vector3[] new_vertices = new Vector3[num_triangles * 3];
                Vector3[] new_normals = new Vector3[num_triangles * 3];
                Vector2[] new_uvs = new Vector2[num_triangles * 3];

                for (int j = 0; j < num_triangles * 3; j++) {
                    new_vertices[j] = mesh.vertices[triangles[j]];
                    if(normals != null) new_normals[j] = normals[j/3];
                    if (uvs != null) new_uvs[j] = uvs[mapping[j]];
                }
                int[] new_triangles = new int[num_triangles * 3];
                for (int j = 0; j < num_triangles; j++) {
                    new_triangles[(j * 3) + 0] = (j * 3) + 0;
                    new_triangles[(j * 3) + 1] = (j * 3) + 2;
                    new_triangles[(j * 3) + 2] = (j * 3) + 1;
                }
                Mesh meshUnity = new Mesh();
                meshUnity.vertices = new_vertices;
                if(normals != null) meshUnity.normals = new_normals;
                meshUnity.triangles = new_triangles;
                if (uvs != null) meshUnity.uv = new_uvs;
				if (normals == null) meshUnity.RecalculateNormals();
                MeshFilter mf = gao.AddComponent<MeshFilter>();
                mf.mesh = meshUnity;
                MeshRenderer mr = gao.AddComponent<MeshRenderer>();
                MeshCollider mc = gao.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;

                mr.material = MapLoader.Loader.collideMaterial;
                if (gameMaterial != null && gameMaterial.collideMaterial != null) {
                    gameMaterial.collideMaterial.SetMaterial(mr);
                }
                if (mesh.type != CollideType.None) {
                    Color col = mr.material.color;
                    mr.material = MapLoader.Loader.collideTransparentMaterial;
                    mr.material.color = new Color(col.r, col.g, col.b, col.a * 0.7f);
                    switch (mesh.type) {
                        case CollideType.ZDD:
                            mr.material.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/zdd")); break;
                        case CollideType.ZDE:
                            mr.material.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/zde")); break;
                        case CollideType.ZDM:
                            mr.material.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/zdm")); break;
                        case CollideType.ZDR:
                            mr.material.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/zdr")); break;
                    }
                }
            }
        }

        public static CollideMeshElement Read(Reader reader, Pointer offset, CollideMeshObject m) {
            MapLoader l = MapLoader.Loader;
            CollideMeshElement sm = new CollideMeshElement(offset, m);
            //l.print(offset + " - " + m.num_vertices);
            sm.off_material = Pointer.Read(reader);
			if (Settings.s.game == Settings.Game.R2Revolution) {
				sm.num_triangles = reader.ReadUInt16();
				reader.ReadUInt16();
				sm.off_triangles = Pointer.Read(reader);
			} else {
				if (Settings.s.engineVersion < Settings.EngineVersion.R3) {
					sm.num_triangles = reader.ReadUInt16();
					sm.num_mapping = reader.ReadUInt16();
					sm.off_triangles = Pointer.Read(reader);
					sm.off_mapping = Pointer.Read(reader);
					sm.off_normals = Pointer.Read(reader);
					sm.off_uvs = Pointer.Read(reader);
					if (Settings.s.engineVersion == Settings.EngineVersion.Montreal) {
						reader.ReadUInt32();
					}
					if (Settings.s.game != Settings.Game.TTSE) {
						Pointer.Read(reader); // table of num_unk vertex indices (vertices, because max = num_vertices - 1)
						reader.ReadUInt16(); // num_unk
						reader.ReadUInt16();
					}
				} else {
					sm.off_triangles = Pointer.Read(reader);
					sm.off_normals = Pointer.Read(reader);
					sm.num_triangles = reader.ReadUInt16();
					reader.ReadUInt16();
					reader.ReadUInt32();
					sm.off_mapping = Pointer.Read(reader);
					sm.off_unk = Pointer.Read(reader); // num_mapping_entries * 3 floats 
					sm.off_unk2 = Pointer.Read(reader); // num_mapping_entries * 1 float
					sm.num_mapping = reader.ReadUInt16();
					reader.ReadUInt16();
				}
			}

            if(sm.off_material != null) sm.gameMaterial = GameMaterial.FromOffsetOrRead(sm.off_material, reader);
            Pointer.Goto(ref reader, sm.off_triangles);
            sm.triangles = new int[sm.num_triangles * 3];
            for (int j = 0; j < sm.num_triangles; j++) {
                sm.triangles[(j * 3) + 0] = reader.ReadInt16();
                sm.triangles[(j * 3) + 1] = reader.ReadInt16();
                sm.triangles[(j * 3) + 2] = reader.ReadInt16();
            }
			Pointer.DoAt(ref reader, sm.off_normals, () => {
				sm.normals = new Vector3[sm.num_triangles];
				for (int j = 0; j < sm.num_triangles; j++) {
					float x = reader.ReadSingle();
					float z = reader.ReadSingle();
					float y = reader.ReadSingle();
					sm.normals[j] = new Vector3(x, y, z);
				}
			});

            if (sm.num_mapping > 0 && sm.off_mapping != null) {
                Pointer.Goto(ref reader, sm.off_mapping);
                sm.mapping = new int[sm.num_triangles * 3];
                for (int i = 0; i < sm.num_triangles; i++) {
                    sm.mapping[(i * 3) + 0] = reader.ReadInt16();
                    sm.mapping[(i * 3) + 1] = reader.ReadInt16();
                    sm.mapping[(i * 3) + 2] = reader.ReadInt16();
                }
                if (sm.off_uvs != null) {
                    Pointer.Goto(ref reader, sm.off_uvs);
                    sm.uvs = new Vector2[sm.num_mapping];
                    for (int i = 0; i < sm.num_mapping; i++) {
                        sm.uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    }
                }
            }

            /*R3Pointer.Goto(ref reader, sm.off_mapping);
            sm.mapping = new int[sm.num_triangles * 3];
            for (int j = 0; j < sm.num_triangles; j++) {
                sm.mapping[(j * 3) + 0] = reader.ReadInt16();
                sm.mapping[(j * 3) + 1] = reader.ReadInt16();
                sm.mapping[(j * 3) + 2] = reader.ReadInt16();
            }
            R3Pointer.Goto(ref reader, sm.off_unk);
            sm.normals = new Vector3[sm.num_mapping_entries];
            for (int j = 0; j < sm.num_mapping_entries; j++) {
                float x = reader.ReadSingle();
                float z = reader.ReadSingle();
                float y = reader.ReadSingle();
                sm.normals[j] = new Vector3(x, y, z);
            }*/
            return sm;
        }

        // Call after clone
        public void Reset() {
            gao = null;
        }

        public ICollideGeometricElement Clone(CollideMeshObject mesh) {
            CollideMeshElement sm = (CollideMeshElement)MemberwiseClone();
            sm.mesh = mesh;
            sm.Reset();
            return sm;
        }
    }
}
